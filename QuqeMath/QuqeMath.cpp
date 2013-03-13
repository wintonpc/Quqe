// QuqeMath.cpp : Defines the exported functions for the DLL application.
//
#include "stdafx.h"
#include <stdio.h>
#include "QuqeMath.h"
#include "LinReg.h"

unsigned int Sig(unsigned char *data, int len)
{
	unsigned int sum = 0;
	for (int i=0; i<len; i++)
		sum = sum * 37 + data[i];
	return sum;
}

unsigned int Sig(unsigned int *sigs, int len)
{
	unsigned int sum = 0;
	for (int i=0; i<len; i++)
		sum = sum * 37 + sigs[i];
	return sum;
}

unsigned int Sig(double* ds, int len)
{
	return Sig((unsigned char*)ds, len * sizeof(double));
}

unsigned int Sig(Matrix* m)
{
	if (m == NULL)
		return 0;
	return Sig(m->Data, m->DataLen);
}

unsigned int Sig(Vector* v)
{
	if (v == NULL)
		return 0;
	return Sig(v->Data, v->Count);
}

unsigned int Sig(Layer* x)
{
	unsigned int sigs[7];
	sigs[0] = Sig(x->a);
	sigs[1] = Sig(x->d);
	sigs[2] = Sig(x->x);
	sigs[3] = Sig(x->z);
	sigs[4] = Sig(x->W);
	sigs[5] = Sig(x->Wr);
	sigs[6] = Sig(x->Bias);
	return Sig(sigs, 7);
}

unsigned int Sig(Layer** xs, int len)
{
	unsigned int* sigs = new unsigned int[len];
	for (int i=0; i<len; i++)
		sigs[i] = Sig(xs[i]);
	unsigned int result = Sig(sigs, len);
	delete [] sigs;
	return result;
}

unsigned int Sig(Frame* x)
{
	return Sig(x->Layers, x->NumLayers);
}

unsigned int Sig(Frame** xs, int len)
{
	unsigned int* sigs = new unsigned int[len];
	for (int i=0; i<len; i++)
		sigs[i] = Sig(xs[i]);
	unsigned int result = Sig(sigs, len);
	delete [] sigs;
	return result;
}

unsigned int Sig(TrainingContext* x)
{
	unsigned int sigs[3];
	sigs[0] = Sig(x->TrainingInput);
	sigs[1] = Sig(x->TrainingOutput);
	sigs[2] = Sig(x->Frames, x->NumFrames);
	return Sig(sigs, 3);
}

void Dump(const char* name, unsigned int sig)
{
	char str[1024];
	sprintf_s(str, 1024, "%s %x\n", name, sig);
	OutputDebugStringA(str);
}

TrainingContext::TrainingContext(const Matrix &trainingInput, const Vector &trainingOutput, int nFrames, Frame** frames, int nLayers,
																 LayerSpec* specs)
{
	TrainingInput = new Matrix(trainingInput);
	TrainingOutput = new Vector(trainingOutput);
	NumFrames = nFrames;
	Frames = frames;
	NumLayers = nLayers;
	LayerSpecs = new LayerSpec[nLayers];
	memcpy(LayerSpecs, specs, nLayers * sizeof(LayerSpec));
	EvalCount = 0;
	//TempVecCount = 524288;
	//TempVecs = new Vector*[TempVecCount];
	//memset(TempVecs, 0, TempVecCount * sizeof(Vector*));
}

TrainingContext::~TrainingContext()
{
	//for (int l = 0; l < NumLayers; l++)
	//{
	//  delete Frames[0]->Layers[l]->W;
	//  delete Frames[0]->Layers[l]->Bias;
	//  if (Frames[0]->Layers[l]->Wr != NULL)
	//    delete Frames[0]->Layers[l]->Wr;
	//}
	//int t_max = TrainingInput->ColumnCount;
	//for (int t = 0; t < t_max; t++)
	//  delete Frames[t];
	//delete [] Frames;
	DeleteFrames(Frames, TrainingInput->ColumnCount);
	delete [] LayerSpecs;
	delete TrainingInput;
	delete TrainingOutput;
	//for (int i = 0; i < TempVecCount; i++)
	//  delete TempVecs[i];
	//delete [] TempVecs;
}

Layer::Layer(Matrix* w, Matrix* wr, Vector* bias, bool isRecurrent, int activationType)
{
	W = w;
	Wr = wr;
	Bias = bias;
	NodeCount = W->RowCount;
	InputCount = W->ColumnCount;
	x = new Vector(InputCount);
	x->Zero();
	a = new Vector(NodeCount);
	a->Zero();
	z = new Vector(NodeCount);
	z->Zero();
	d = new Vector(NodeCount);
	d->Zero();
	IsRecurrent = isRecurrent;
	ActivationType = activationType;
}

void Layer::DeleteWeights()
{
	delete W;
	if (Wr != NULL)
		delete Wr;
	delete Bias;
}

Layer::~Layer()
{
	delete x;
	delete a;
	delete z;
	delete d;
}

Frame::Frame(Layer** layers, int numLayers)
{
	Layers = layers;
	NumLayers = numLayers;
}

Frame::~Frame()
{
	if (Layers != NULL)
		//{
			DeleteLayers(Layers, NumLayers, false);
	//for (int l = 0; l < NumLayers; l++)
	//  delete Layers[l];
	//delete [] Layers;
	//}
}

QUQEMATH_API void* CreateTrainingContext(
	LayerSpec* layerSpecs, int nLayers,
	double* trainingData, double* outputData,
	int nInputs, int nSamples)
{
	//unsigned char bytes[5] = { 1,2,3,4,5};
	//Dump("initial", Sig(bytes, 4));
	//bytes[0] = 100;
	//Dump("changed", Sig(bytes, 4));
	//bytes[3] = 101;
	//Dump("changed", Sig(bytes, 4));
	//bytes[4] = 101;
	//Dump("same   ", Sig(bytes, 4));

	//Frame** frames = new Frame*[nSamples];
	//for (int t = 0; t < nSamples; t++)
	//{
	//  Layer** layers = new Layer*[nLayers];
	//  for (int l = 0; l < nLayers; l++)
	//  {
	//    Layer* pl = protoLayers[l];
	//    layers[l] = new Layer(pl->W, pl->Wr, pl->Bias, pl->IsRecurrent, pl->ActivationType,
	//      pl->ActivationFunction, pl->ActivationFunctionPrime);
	//  }
	//  frames[t] = new Frame(layers, nLayers);
	//}

	Layer** protoLayers = SpecsToLayers(nInputs, layerSpecs, nLayers);
	//Dump("CreateTrainingContext:protoLayers", Sig(protoLayers, nLayers));
	Frame** frames = LayersToFrames(protoLayers, nLayers, nSamples);
	//Dump("CreateTrainingContext:frames", Sig(frames, nSamples));

	DeleteLayers(protoLayers, nLayers, false);

	TrainingContext* context = new TrainingContext(Matrix(nInputs, nSamples, trainingData), Vector(nSamples, outputData), nSamples, frames, nLayers, layerSpecs);
	//Dump("CreateTrainingContext:context", Sig(context));

	return context;
}



QUQEMATH_API void* CreatePropagationContext(LayerSpec* layerSpecs, int nLayers, int nInputs, double* weights, int nWeights)
{
	Layer** protoLayers = SpecsToLayers(nInputs, layerSpecs, nLayers);
	Frame** frames = LayersToFrames(protoLayers, nLayers, 1);
	Frame* theFrame = frames[0];

	SetWeights(theFrame->Layers, nLayers, weights, nWeights);

	DeleteLayers(protoLayers, nLayers, false);

	return frames;
}

QUQEMATH_API void DestroyPropagationContext(void* context)
{
	DeleteFrames((Frame**)context, 1);
}

Frame** LayersToFrames(Layer** protoLayers, int nLayers, int nSamples)
{
	Frame** frames = new Frame*[nSamples];
	for (int t = 0; t < nSamples; t++)
	{

		Layer** layers = new Layer*[nLayers];
		for (int l = 0; l < nLayers; l++)
		{
			Layer* pl = protoLayers[l];
			layers[l] = new Layer(pl->W, pl->Wr, pl->Bias, pl->IsRecurrent, pl->ActivationType);
		}
		frames[t] = new Frame(layers, nLayers);
	}
	return frames;
}

void DeleteFrames(Frame** frames, int nSamples)
{
	int nLayers = frames[0]->NumLayers;
	for (int l = 0; l < nLayers; l++)
	{
		frames[0]->Layers[l]->DeleteWeights();
		//delete frames[0]->Layers[l]->W;
		//delete frames[0]->Layers[l]->Bias;
		//if (frames[0]->Layers[l]->Wr != NULL)
		//  delete frames[0]->Layers[l]->Wr;
	}
	int t_max = nSamples;
	for (int t = 0; t < t_max; t++)
		delete frames[t];
	delete [] frames;
}

// delete everything but the weights. those are deleted by DeleteFrames()
void DeleteLayers(Layer** layers, int nLayers, bool deleteWeights)
{
	for (int l = 0; l < nLayers; l++)
	{
		if (deleteWeights)
			layers[l]->DeleteWeights();
		delete layers[l];
	}
	delete [] layers;
}

Layer** SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers)
{
	Layer** layers = new Layer*[numLayers];
	for (int l = 0; l < numLayers; l++)
	{
		LayerSpec* s = &specs[l];

		Matrix* w = new Matrix(s->NodeCount, l > 0 ? specs[l-1].NodeCount : numInputs);
		w->Zero();
		Matrix* wr = NULL;
		if (s->IsRecurrent)
		{
			wr = new Matrix(s->NodeCount, s->NodeCount);
			wr->Zero();
		}
		Vector* bias = new Vector(s->NodeCount);
		bias->Zero();
		layers[l] = new Layer(w, wr, bias, s->IsRecurrent, s->ActivationType);
	}
	return layers;
}

QUQEMATH_API void DestroyTrainingContext(void* context)
{
	//Dump("TotalEvalCount", ((TrainingContext*)context)->EvalCount);
	delete ((TrainingContext*)context);
}

QUQEMATH_API void PropagateInput(Frame** frames, double* input, double* output)
{
	Frame* theFrame = frames[0];
	Propagate(input, 1, theFrame->NumLayers, theFrame->Layers, theFrame->Layers);
	Layer* lastLayer = theFrame->Layers[theFrame->NumLayers-1];
	memcpy(output, lastLayer->z->Data, lastLayer->NodeCount * sizeof(double));
}

QUQEMATH_API void EvaluateWeights(TrainingContext* c, double* weights, int nWeights, double* output, double* error, double* gradient)
{
	c->EvalCount++;
	bool shouldDump = c->EvalCount == 4 || c->EvalCount == 5;
	if (shouldDump)
	{
		Dump("EvaluateWeights:EvalCount", c->EvalCount);
		Dump("EvaluateWeights:weights initial", Sig(weights, nWeights));
	}

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

	//if (shouldDump)
	//	Dump("EvaluateWeights:context after forward prop", Sig(c));

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
		//delete layer->W;
		//delete layer->Wr;
		//delete layer->Bias;
		delete layer;
	}
	delete [] gradLayers;

	if (shouldDump)
	{
		//Dump("EvaluateWeights:weights final", Sig(weights, nWeights));
		Dump("EvaluateWeights:output final", Sig(output, lastLayer->NodeCount));
		Dump("EvaluateWeights:error final", Sig(error, 1));
		Dump("EvaluateWeights:gradient final", Sig(gradient, nWeights));
	}
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

void PropagateLayer(double* input, int inputStride, Layer* layer, Vector* recurrentInput)
{
	int inputCount = layer->InputCount;

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
	//Frame** frames = LayersToFrames(layers, nLayers, 1);
	int nWeights = GetWeights(layers, nLayers, BigNonThreadsafeTempArray, -1);
	//DeleteFrames(frames, 1);
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

OrthoContext::OrthoContext(int basisDimension, int maxBasisCount)
{
	Pv = new Vector(basisDimension);
	Bases = new Matrix(maxBasisCount, basisDimension);
	Dp = new Vector(maxBasisCount);
}

OrthoContext::~OrthoContext()
{
	delete Pv;
	delete Bases;
	delete Dp;
}

QUQEMATH_API void* CreateOrthoContext(int basisDimension, int maxBasisCount)
{
	return new OrthoContext(basisDimension, maxBasisCount);
}

QUQEMATH_API void DestroyOrthoContext(void* context)
{
	delete ((OrthoContext*)context);
}

void Orthogonalize(OrthoContext* c, double* p, int numBases, double* orthonormalBases)
{
	memcpy(c->Pv->Data, p, c->Pv->Count * sizeof(double));
	memcpy(c->Bases->Data, orthonormalBases, numBases * c->Pv->Count * sizeof(double));
	c->Bases->RowCount = numBases;
	c->Dp->Count = numBases;

	int basisLen = c->Pv->Count;
	GEMV(1, c->Bases, c->Pv, 0, c->Dp);
	for (int i = 0, offset = 0; i < numBases; i++, offset += basisLen)
		AXPY2(-1 * c->Dp->Data[i], c->Bases->Data + offset, basisLen, c->Pv->Data);
	double mag = cblas_dnrm2(basisLen, c->Pv->Data, 1);
	cblas_dscal(basisLen, 1.0 / mag, c->Pv->Data, 1);
	memcpy(p, c->Pv->Data, basisLen * sizeof(double));
}
