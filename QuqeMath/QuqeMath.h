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

const int ACTIVATION_LOGSIG = 0;
const int ACTIVATION_PURELIN = 1;
const double TimeZeroRecurrentInputValue = 0.5;

struct LayerSpec
{
  int NodeCount;
  bool IsRecurrent;
  int ActivationType;
};

struct Matrix
{
  int RowCount;
  int ColumnCount;
  double* Data;
};

struct Vector
{
  int Count;
  double* Data;
};

typedef double(*ActivationFunc)(double);

struct Layer
{
	Matrix W;
  Matrix Wr;
  Vector Bias;
  Vector x;
  Vector a;
  Vector z;
  Vector d;
  bool IsRecurrent;
  ActivationFunc ActivationFunction;
  ActivationFunc ActivationFunctionPrime;
};

struct Frame
{
  Layer* Layers;
};

int NodeCount(Layer* layer)
{
  return layer->W.RowCount;
}

int InputCount(Layer* layer)
{
  return layer->W.ColumnCount;
}

extern "C" QUQEMATH_API void EvaluateWeights(
  LayerSpec* layerSpecs, int numLayers, Vector weights, Matrix trainingData, Vector outputData, // in
  double* error, double* gradient // out
  );

Layer* SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers);
double* MakeTimeZeroRecurrentInput(int size);

void SetWeightVector(Layer* layers, int numLayers, Vector weights);
void SetVectorWeights(const Vector &v, int* wi, double* weights);
void SetMatrixWeights(const Matrix &m, int* wi, double* weights);
void SetMatrix(const Matrix &m, int i, int j, double v);

double Linear(double x);
double LinearPrime(double x);
double LogisticSigmoid(double x);
double LogisticSigmoidPrime(double x);
