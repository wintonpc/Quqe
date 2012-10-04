// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the QUQEMATH_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// QUQEMATH_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef QUQEMATH_EXPORTS
#define QUQEMATH_API __declspec(dllexport)
#else
#define QUQEMATH_API __declspec(dllimport)
#endif

#include "LinReg.h"

const int ACTIVATION_LOGSIG = 0;
const int ACTIVATION_PURELIN = 1;
const double TimeZeroRecurrentInputValue = 0.5;

struct LayerSpec
{
  int NodeCount;
  bool IsRecurrent;
  int ActivationType;
};

typedef double(*ActivationFunc)(double);

class Layer
{
public:
	Matrix* W;
  Matrix* Wr;
  Vector* Bias;
  Vector* x;
  Vector* a;
  Vector* z;
  Vector* d;
  bool IsRecurrent;
  ActivationFunc ActivationFunction;
  ActivationFunc ActivationFunctionPrime;

  Layer()
  {
    W = NULL;
    Wr = NULL;
    Bias = NULL;
    x = NULL;
    a = NULL;
    z = NULL;
    d = NULL;
  }

  int NodeCount()
  {
    return W->RowCount;
  }

  int InputCount()
  {
    return W->ColumnCount;
  }

  ~Layer()
  {
    if (W != NULL) delete W;
    if (Wr != NULL) delete Wr;
    if (Bias != NULL) delete Bias;
    if (x != NULL) delete x;
    if (a != NULL) delete a;
    if (z != NULL) delete z;
    if (d != NULL) delete d;
  }
};

class Frame
{
public:
  Layer* Layers;
  int NumLayers;

  ~Frame()
  {
    if (Layers != NULL)
      delete [] Layers;
  }
};

extern "C" QUQEMATH_API void EvaluateWeights(
  // in
  LayerSpec* layerSpecs, int nLayers,
  double* weights, int nWeights,
  double* trainingData, double* outputData,
  int nInputs, int nSamples,
  // out
  double* error, double* gradient
  );

void Propagate(Vector* input, int numLayers, Layer* currLayers, Layer* prevLayers);
void PropagateLayer(Vector* input, Layer* layer, Vector* recurrentInput);
Vector* ApplyActivationFunction(Vector* a, ActivationFunc f);
Layer* SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers);
Vector* MakeTimeZeroRecurrentInput(int size);

void SetWeightVector(Layer* layers, int numLayers, const Vector &weights);
double* SetVectorWeights(Vector* v, double* weights);
double* SetMatrixWeights(Matrix* m, double* weights);

double Linear(double x);
double LinearPrime(double x);
double LogisticSigmoid(double x);
double LogisticSigmoidPrime(double x);
