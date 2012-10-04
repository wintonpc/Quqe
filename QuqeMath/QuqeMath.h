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
  int NodeCount;
  int InputCount;

  Layer(Matrix* w, Matrix* wr, Vector* bias, bool isRecurrent, ActivationFunc activation,
    ActivationFunc activationPrime);
  ~Layer();
};

class Frame
{
public:
  Layer** Layers;
  int NumLayers;

  Frame(Layer** layers, int numLayers);
  ~Frame();
};

class WeightContext
{
public:
  Matrix* TrainingInput;
  Vector* TrainingOutput;
  Frame** Frames;
  int NumLayers;

  WeightContext(const Matrix &trainingInput, const Vector &trainingOutput, Frame** frames, int nLayers);
  ~WeightContext();
};

extern "C" QUQEMATH_API void* CreateWeightContext(
  LayerSpec* layerSpecs, int nLayers,
  double* trainingData, double* outputData,
  int nInputs, int nSamples);

extern "C" QUQEMATH_API void DestroyWeightContext(void* context);

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
Layer** SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers);
Vector* MakeTimeZeroRecurrentInput(int size);

void SetWeightVector(Layer* layers, int numLayers, const Vector &weights);
double* SetVectorWeights(Vector* v, double* weights);
double* SetMatrixWeights(Matrix* m, double* weights);

double Linear(double x);
double LinearPrime(double x);
double LogisticSigmoid(double x);
double LogisticSigmoidPrime(double x);
