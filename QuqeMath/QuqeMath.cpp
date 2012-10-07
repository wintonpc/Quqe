// QuqeMath.cpp : Defines the exported functions for the DLL application.
//
#include "stdafx.h"
#include "QuqeMath.h"
#include "LinReg.h"

WeightContext::WeightContext(const Matrix &trainingInput, const Vector &trainingOutput, Frame** frames, int nLayers,
  LayerSpec* specs)
{
  TrainingInput = new Matrix(trainingInput);
  TrainingOutput = new Vector(trainingOutput);
  Frames = frames;
  NumLayers = nLayers;
  LayerSpecs = new LayerSpec[nLayers];
  memcpy(LayerSpecs, specs, nLayers * sizeof(LayerSpec));
  TempVecCount = 524288;
  TempVecs = new Vector*[TempVecCount];
  memset(TempVecs, 0, TempVecCount * sizeof(Vector*));
}

WeightContext::~WeightContext()
{
  for (int l = 0; l < NumLayers; l++)
  {
    delete Frames[0]->Layers[l]->W;
    delete Frames[0]->Layers[l]->Bias;
    if (Frames[0]->Layers[l]->Wr != NULL)
      delete Frames[0]->Layers[l]->Wr;
  }
  int t_max = TrainingInput->ColumnCount;
  for (int t = 0; t < t_max; t++)
    delete Frames[t];
  delete [] Frames;
  delete [] LayerSpecs;
  delete TrainingInput;
  delete TrainingOutput;
  for (int i = 0; i < TempVecCount; i++)
    delete TempVecs[i];
  delete [] TempVecs;
}

Layer::Layer(Matrix* w, Matrix* wr, Vector* bias, bool isRecurrent, ActivationFunc activation, ActivationFunc activationPrime)
{
  W = w;
  Wr = wr;
  Bias = bias;
  NodeCount = W->RowCount;
  InputCount = W->ColumnCount;
  x = new Vector(InputCount);
  a = new Vector(NodeCount);
  z = new Vector(NodeCount);
  d = new Vector(NodeCount);
  IsRecurrent = isRecurrent;
  ActivationFunction = activation;
  ActivationFunctionPrime = activationPrime;
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
  {
    for (int l = 0; l < NumLayers; l++)
      delete Layers[l];
    delete [] Layers;
  }
}

QUQEMATH_API void* CreateWeightContext(
  LayerSpec* layerSpecs, int nLayers,
  double* trainingData, double* outputData,
  int nInputs, int nSamples)
{
  Layer** protoLayers = SpecsToLayers(nInputs, layerSpecs, nLayers);
  Frame** frames = new Frame*[nSamples];
  for (int t = 0; t < nSamples; t++)
  {
    Layer** layers = new Layer*[nLayers];
    for (int l = 0; l < nLayers; l++)
    {
      Layer* pl = protoLayers[l];
      layers[l] = new Layer(pl->W, pl->Wr, pl->Bias, pl->IsRecurrent, pl->ActivationFunction, pl->ActivationFunctionPrime);
    }
    frames[t] = new Frame(layers, nLayers);
  }

  for (int l = 0; l < nLayers; l++)
    delete protoLayers[l];
  delete [] protoLayers;

  return new WeightContext(Matrix(nInputs, nSamples, trainingData), Vector(nSamples, outputData), frames, nLayers,
    layerSpecs);
}

Layer** SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers)
{
  Layer** layers = new Layer*[numLayers];
  for (int l = 0; l < numLayers; l++)
  {
    LayerSpec* s = &specs[l];

    ActivationFunc activation;
    ActivationFunc activationPrime;
    if (s->ActivationType == ACTIVATION_LOGSIG)
    {
      activation = LogisticSigmoid;
      activationPrime = LogisticSigmoidPrime;
    }
    else if (s->ActivationType == ACTIVATION_PURELIN)
    {
      activation = Linear;
      activationPrime = LinearPrime;
    }

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
    layers[l] = new Layer(w, wr, bias, s->IsRecurrent, activation, activationPrime);
  }
  return layers;
}

QUQEMATH_API void DestroyWeightContext(void* context)
{
  delete ((WeightContext*)context);
}

QUQEMATH_API void EvaluateWeights(WeightContext* c, double* weights, int nWeights, double* output, double* error, double* gradient)
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
  Vector* input = c->GetTempVec(trainingInput->RowCount);
  for (int t = 0; t <= t_max; t++)
  {
    trainingInput->GetColumn(t, input);
    Propagate(input, numLayers, time[t]->Layers, t > 0 ? time[t-1]->Layers : NULL);
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
          err = DotColumn(subsequentLayer->W, i, subsequentLayer->d);
        }

        // calculate error propagated forward in time (recurrently)
        if (t < t_max && layer->IsRecurrent)
        {
          Layer* nextLayerInTime = time[t + 1]->Layers[l];
          err += DotColumn(nextLayerInTime->Wr, i, nextLayerInTime->d);
        }

        layer->d->Data[i] = err * layer->ActivationFunctionPrime(layer->a->Data[i]);
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
      for (int i = 0; i < nodeCount; i++)
      {
        double* dData = layertl->d->Data;
        double* wi = gradLayers[l]->W->GetRowPtr(i);
        AXPY(-1.0 * dData[i], layertl->x, wi);
      }

      // Wr
      if (t > 0 && gradLayers[l]->IsRecurrent)
      {
        Layer* layert1l = time[t - 1]->Layers[l];
        for (int i = 0; i < nodeCount; i++)
        {
          double* dData = layertl->d->Data;
          double* wri = gradLayers[l]->Wr->GetRowPtr(i);
          AXPY(-1.0 * dData[i], layert1l->z, wri);
        }
      }

      // Bias
      AXPY(-1.0, layertl->d, gradLayers[l]->Bias->Data);
    }
  }
  GetWeights(gradLayers, numLayers, gradient, nWeights);
  for (int l = 0; l < numLayers; l++)
  {
    Layer* layer = gradLayers[l];
    delete layer->W;
    delete layer->Wr;
    delete layer->Bias;
    delete layer;
  }
  delete [] gradLayers;
}

void Propagate(Vector* input, int numLayers, Layer** currLayers, Layer** prevLayers)
{
  for (int l = 0; l < numLayers; l++)
  {
    Layer* layer = currLayers[l];
    PropagateLayer(input, layer, prevLayers != NULL ? prevLayers[l]->z : NULL);
    input = layer->z;
  }
}

void PropagateLayer(Vector* input, Layer* layer, Vector* recurrentInput)
{
  // set x
  layer->x->Set(input);

  // compute a
  layer->a->Set(layer->Bias);
  GEMV(1, layer->W, input, 1, layer->a);
  if (layer->IsRecurrent)
  {
    Vector* ri = recurrentInput != NULL ? recurrentInput : MakeTimeZeroRecurrentInput(layer->NodeCount);
    GEMV(1, layer->Wr, ri, 1, layer->a);
    if (recurrentInput == NULL)
      delete ri;
  }

  // compute z
  layer->z->Set(layer->a);
  ApplyActivationFunction(layer->z, layer->ActivationFunction);
}

void ApplyActivationFunction(Vector* z, ActivationFunc f)
{
  int len = z->Count;
  double* zData = z->Data;
  for (int i = 0; i < len; i++)
    zData[i] = f(zData[i]);
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

void GetWeights(Layer** layers, int numLayers, double* weights, int nWeights)
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
  assert(weights + nWeights == dp);
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

double Linear(double x)
{
  return x;
}

double LinearPrime(double x)
{
  return 1;
}

double LogisticSigmoid(double x)
{
  return 1 / (1 + exp(-x));
}

double LogisticSigmoidPrime(double x)
{
  double v = LogisticSigmoid(x);
  return v * (1 - v);
}
