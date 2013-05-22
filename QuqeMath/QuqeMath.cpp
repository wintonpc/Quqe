#include "stdafx.h"
#include <stdio.h>
#include <float.h>
#include "QuqeMath.h"
#include "LinReg.h"

QUQEMATH_API void EvaluateWeights(TrainingContext* c, double* weights, int nWeights, double* output, double* error, double* gradient)
{
	int netNumInputs = c->Frames[0]->Layers[0]->W->ColumnCount;
	int t_max = c->TrainingInput->ColumnCount - 1;
	Frame** time = c->Frames;
	double totalOutputError = 0;

	Matrix* trainingInput = c->TrainingInput;
	Vector* trainingOutput = c->TrainingOutput;
	int numLayers = c->NumLayers;

	// propagate inputs forward
	SetWeights(time[0]->Layers, numLayers, weights, nWeights); // time[0] is sufficient since all share the same weight matrices/vectors
	for (int t = 0; t <= t_max; t++)
	{
		Propagate(GetColumnPtr(trainingInput, t), trainingInput->ColumnCount, numLayers,
			time[t]->Layers, t > 0 ? time[t-1]->Layers : NULL);
	}
	Layer* lastLayer = time[t_max]->Layers[numLayers-1];
	memcpy(output, lastLayer->z->Data, lastLayer->NodeCount * sizeof(double));

	// propagate error backward
	for (int t = t_max; t >= 0; t--)
	{
		int l_max = numLayers - 1;
		for (int l = l_max; l >= 0; l--)
		{
			Layer* layer = time[t]->Layers[l];
			for (int i = 0; i < layer->NodeCount; i++)
			{
				double err;

				// calculate error propagated to next layer
				if (l == l_max)
				{
					err = (trainingOutput->Data[t] - layer->z->Data[i]);
					totalOutputError += 0.5 * pow(err, 2);
				}
				else
				{
					Layer* subsequentLayer = time[t]->Layers[l + 1];
					Matrix* w = subsequentLayer->W;
					Vector* d = subsequentLayer->d;
					err = DotColumn(w, i, d);
				}

				// calculate error propagated forward in time (recurrently)
				if (t < t_max && layer->IsRecurrent)
				{
					Layer* nextLayerInTime = time[t + 1]->Layers[l];
					Matrix* wr = nextLayerInTime->Wr;
					Vector* d = nextLayerInTime->d;
					err += DotColumn(wr, i, d);
				}

				if (layer->ActivationType == ACTIVATION_LOGSIG)
					layer->d->Data[i] = err * LogisticSigmoidPrime(layer->a->Data[i]);
				else // ACTIVATION_PURELIN
					layer->d->Data[i] = err;
			}
		}
	}
	*error = totalOutputError;

	// calculate gradient
	Layer** gradLayers = SpecsToLayers(netNumInputs, c->LayerSpecs, numLayers);
	for (int t = 0; t <= t_max; t++)
	{
		for (int l = 0; l < numLayers; l++)
		{
			int nodeCount = gradLayers[l]->NodeCount;
			int inputCount = gradLayers[l]->InputCount;

			// W
			Layer* layertl = time[t]->Layers[l];
			GER(-1.0, layertl->d->Data, layertl->x->Data, gradLayers[l]->W);

			// Wr
			if (t > 0 && gradLayers[l]->IsRecurrent)
			{
				Layer* layert1l = time[t - 1]->Layers[l];
				GER(-1.0, layertl->d->Data, layert1l->z->Data, gradLayers[l]->Wr);
			}

			// Bias
			Vector* layertld = layertl->d;
			AXPY(-1.0, layertld, layertld->Count, gradLayers[l]->Bias->Data);
		}
	}
	GetWeights(gradLayers, numLayers, gradient, nWeights);
	for (int l = 0; l < numLayers; l++)
	{
		Layer* layer = gradLayers[l];
		layer->DeleteWeights();
		delete layer;
	}
	delete [] gradLayers;
}

QUQEMATH_API void PropagateInput(Frame** frames, double* input, double* output)
{
	Frame* theFrame = frames[0];
	Propagate(input, 1, theFrame->NumLayers, theFrame->Layers, theFrame->Layers);
	Layer* lastLayer = theFrame->Layers[theFrame->NumLayers-1];
	memcpy(output, lastLayer->z->Data, lastLayer->NodeCount * sizeof(double));
}

void Propagate(double* input, int inputStride, int numLayers, Layer** currLayers, Layer** prevLayers)
{
	Layer* layer0 = currLayers[0];
	PropagateLayer(input, inputStride, layer0, prevLayers != NULL ? prevLayers[0]->z : NULL);
	input = layer0->z->Data;
	for (int l = 1; l < numLayers; l++)
	{
		Layer* layer = currLayers[l];
		PropagateLayer(input, 1, layer, prevLayers != NULL ? prevLayers[l]->z : NULL);
		input = layer->z->Data;
	}
}

void AssertNoNaNs(double* xs, int count)
{
	for (int i=0; i<count; i++)
		assert(!_isnan(xs[i]));
}

void PropagateLayer(double* input, int inputStride, Layer* layer, Vector* recurrentInput)
{
	int inputCount = layer->InputCount;
	
#if DEBUG
	AssertNoNaNs(input, inputCount);
	if (recurrentInput != NULL)
		AssertNoNaNs(recurrentInput->Data, recurrentInput->Count);
#endif

	// set x
	layer->x->Set(input, inputStride, inputCount);

	// compute a
	layer->a->Set(layer->Bias);
	GEMV(1, layer->W, input, inputStride, 1, layer->a);
	if (layer->IsRecurrent)
	{
		Vector* ri = recurrentInput != NULL ? recurrentInput : MakeTimeZeroRecurrentInput(layer->NodeCount);
		GEMV(1, layer->Wr, ri, 1, layer->a);
		if (recurrentInput == NULL)
			delete ri;
	}

	// compute z
	layer->z->Set(layer->a);
	if (layer->ActivationType == ACTIVATION_LOGSIG)
	{
		int len = layer->z->Count;
		double* zData = layer->z->Data;
		for (int i = 0; i < len; i++)
			zData[i] = LogisticSigmoid(zData[i]);
	}
	// else ACTIVATION_PURELIN, in which case zData is already what it should be

#if DEBUG
	AssertNoNaNs(layer->z->Data, layer->z->Count);
#endif
}

void SetWeights(Layer** layers, int numLayers, double* weights, int nWeights)
{
	double* dp = weights;
	for (int layer = 0; layer < numLayers; layer++)
	{
		Layer* l = layers[layer];
		dp = SetMatrixWeights(l->W, dp);
		if (l->IsRecurrent)
			dp = SetMatrixWeights(l->Wr, dp);
		dp = SetVectorWeights(l->Bias, dp);
	}
	assert(weights + nWeights == dp);
}

double* SetVectorWeights(Vector* v, double* weights)
{
	int len = v->Count;
	memcpy(v->Data, weights, len * sizeof(double));
	return weights + len;
}

double* SetMatrixWeights(Matrix* m, double* weights)
{
	int len = m->RowCount * m->ColumnCount;
	memcpy(m->Data, weights, len * sizeof(double));
	return weights + len;
}

double* BigNonThreadsafeTempArray = new double[1024*1024];
int GetWeightCount(LayerSpec* layerSpecs, int nLayers, int nInputs)
{
	Layer** layers = SpecsToLayers(nInputs, layerSpecs, nLayers);
	int nWeights = GetWeights(layers, nLayers, BigNonThreadsafeTempArray, -1);
	DeleteLayers(layers, nLayers, true);
	return nWeights;
}

int GetWeights(Layer** layers, int numLayers, double* weights, int nWeights)
{
	double* dp = weights;
	for (int layer = 0; layer < numLayers; layer++)
	{
		Layer* l = layers[layer];
		dp = GetMatrixWeights(l->W, dp);
		if (l->IsRecurrent)
			dp = GetMatrixWeights(l->Wr, dp);
		dp = GetVectorWeights(l->Bias, dp);
	}
	if (nWeights != -1)
	{
		assert(weights + nWeights == dp);
		assert(nWeights == (dp - weights));
	}
	int result = dp - weights;
	return result;
}

double* GetVectorWeights(Vector* v, double* weights)
{
	int len = v->Count;
	memcpy(weights, v->Data, len * sizeof(double));
	return weights + len;
}

double* GetMatrixWeights(Matrix* m, double* weights)
{
	int len = m->RowCount * m->ColumnCount;
	memcpy(weights, m->Data, len * sizeof(double));
	return weights + len;
}

Vector* MakeTimeZeroRecurrentInput(int size)
{
	Vector* v = new Vector(size);
	double* vData = v->Data;
	for (int i = 0; i < size; i++)
		vData[i] = TimeZeroRecurrentInputValue;
	return v;
}
