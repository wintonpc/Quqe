// QuqeMath.cpp : Defines the exported functions for the DLL application.
//
#include "stdafx.h"
#include "QuqeMath.h"


QUQEMATH_API void EvaluateWeights(
  LayerSpec* layerSpecs, int numLayers, Vector weights, Matrix trainingData, Vector outputData, // in
  double* error, double* gradient // out
  )
{
  int netNumInputs = trainingData.RowCount;
  int t_max = outputData.Count;
  Frame* time = new Frame[t_max];
  double totalOutputError = 0;

  // propagate inputs forward
  for (int t = 0; t < t_max; t++)
  {
    time[t].Layers = SpecsToLayers(netNumInputs, layerSpecs, numLayers);
    SetWeightVector(time[t].Layers, numLayers, weights);
  }

  *error = 42;
  gradient[0] = 1;
  gradient[1] = 2;
  gradient[2] = 3;
}

Layer* SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers)
{
  Layer* layers = new Layer[numLayers];
  for (int l = 0; l < numLayers; l++)
  {
    LayerSpec s = specs[l];
    Layer* layer = &layers[l];
    layer->W.RowCount = s.NodeCount;
    layer->W.ColumnCount = l > 0 ? layers[l-1].z.Count : numInputs;
    layer->W.Data = new double[layer->W.RowCount * layer->W.ColumnCount];
    layer->Bias.Count = s.NodeCount;
    layer->Bias.Data = new double[layer->Bias.Count];

    if (s.IsRecurrent)
    {
      layer->Wr.RowCount = s.NodeCount;
      layer->Wr.ColumnCount = s.NodeCount;
      layer->Wr.Data = new double[layer->Wr.RowCount * layer->Wr.ColumnCount];
      layer->z.Count = s.NodeCount;
      layer->z.Data = MakeTimeZeroRecurrentInput(layer->z.Count);
    }

    if (s.ActivationType == ACTIVATION_LOGSIG)
    {
      layer->ActivationFunction = LogisticSigmoid;
      layer->ActivationFunctionPrime = LogisticSigmoidPrime;
    }
    else if (s.ActivationType == ACTIVATION_PURELIN)
    {
      layer->ActivationFunction = Linear;
      layer->ActivationFunctionPrime = LinearPrime;
    }

    layer->d.Count = s.NodeCount;
    layer->d.Data = new double[layer->d.Count];
  }

  return layers;
}

void SetWeightVector(Layer* layers, int numLayers, Vector weights)
{
  int wi = 0;
  for (int layer = 0; layer < numLayers; layer++)
  {
    Layer l = layers[layer];
    SetMatrixWeights(l.W, &wi, weights.Data);
    if (l.IsRecurrent)
      SetMatrixWeights(l.Wr, &wi, weights.Data);
    SetVectorWeights(l.Bias, &wi, weights.Data);
  }
  assert(wi == weights.Count);
}

void SetVectorWeights(const Vector &v, int* wi, double* weights)
{
  int len = v.Count;
  for (int i = 0; i < len; i++)
    v.Data[i] = weights[(*wi)++];
}

void SetMatrixWeights(const Matrix &m, int* wi, double* weights)
{
  int nRows = m.RowCount;
  int nCols = m.ColumnCount;
  for (int i = 0; i < nRows; i++)
    for (int j = 0; j < nCols; j++)
      SetMatrix(m, i, j, weights[(*wi)++]);
}

void SetMatrix(const Matrix &m, int i, int j, double v)
{
  m.Data[i * m.ColumnCount + j] = v;
}

double* MakeTimeZeroRecurrentInput(int size)
{
  double* a = new double[size];
  for (int i = 0; i < size; i++)
    a[i] = TimeZeroRecurrentInputValue;
  return a;
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


