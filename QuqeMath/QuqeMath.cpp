// QuqeMath.cpp : Defines the exported functions for the DLL application.
//
#include "stdafx.h"
#include "QuqeMath.h"
#include "LinReg.h"

WeightContext::WeightContext(const Matrix &trainingInput, const Vector &trainingOutput, Frame** frames, int nLayers)
{
  TrainingInput = new Matrix(trainingInput);
  TrainingOutput = new Vector(trainingOutput);
  Frames = frames;
  NumLayers = nLayers;
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
  delete TrainingInput;
  delete TrainingOutput;
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

  return new WeightContext(Matrix(nInputs, nSamples, trainingData), Vector(nSamples, outputData), frames, nLayers);
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

    layers[l] = new Layer(
      new Matrix(s->NodeCount, l > 0 ? specs[l-1].NodeCount : numInputs),
      s->IsRecurrent ? new Matrix(s->NodeCount, s->NodeCount) : NULL,
      new Vector(s->NodeCount),
      s->IsRecurrent, activation, activationPrime);
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
  int t_max = c->TrainingInput->ColumnCount;
  Frame** time = c->Frames;
  double totalOutputError = 0;

  Vector w = Vector(nWeights, weights);
  
  // propagate inputs forward
  int numLayers = c->NumLayers;
  SetWeightVector(time[0]->Layers, numLayers, &w); // time[0] is sufficient since all share the same weight matrices/vectors
  Vector input = Vector(c->TrainingInput->RowCount);
  for (int t = 0; t < t_max; t++)
  {
    c->TrainingInput->GetColumn(t, &input);
    Propagate(&input, numLayers, time[t]->Layers, t > 0 ? time[t-1]->Layers : NULL);
  }

  //*error = 42;
  //gradient[0] = 1;
  //gradient[1] = 2;
  //gradient[2] = 3;

  //delete [] time;
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
  Matrix::GEMV(1, layer->W, input, 1, layer->a);
  if (layer->IsRecurrent)
  {
    Vector* ri = recurrentInput != NULL ? recurrentInput : MakeTimeZeroRecurrentInput(layer->NodeCount);
    Matrix::GEMV(1, layer->Wr, ri, 1, layer->a);
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

void SetWeightVector(Layer** layers, int numLayers, Vector* weights)
{
  double* dp = weights->Data;
  for (int layer = 0; layer < numLayers; layer++)
  {
    Layer* l = layers[layer];
    dp = SetMatrixWeights(l->W, dp);
    if (l->IsRecurrent)
      dp = SetMatrixWeights(l->Wr, dp);
    dp = SetVectorWeights(l->Bias, dp);
  }
  assert(weights->Data + weights->Count == dp);
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


