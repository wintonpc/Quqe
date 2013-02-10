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
  int ActivationType;
  int NodeCount;
  int InputCount;

  Layer(Matrix* w, Matrix* wr, Vector* bias, bool isRecurrent, int activationType);
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

class TrainingContext
{
public:
  Matrix* TrainingInput;
  Vector* TrainingOutput;
  Frame** Frames;
  int NumLayers;
  LayerSpec* LayerSpecs;
private:
  Vector** TempVecs;
  int TempVecCount;

public:
  TrainingContext(const Matrix &trainingInput, const Vector &trainingOutput, Frame** frames, int nLayers, LayerSpec* specs);
  ~TrainingContext();
};

class OrthoContext
{
public:
  Vector* Pv;
  Matrix* Bases;
  Vector* Dp;

  OrthoContext(int basisDimension, int maxBasisCount);
  ~OrthoContext();
};

extern "C" {
  
QUQEMATH_API void* CreateTrainingContext(
	LayerSpec* layerSpecs, int nLayers,
	double* trainingData, double* outputData,
	int nInputs, int nSamples);

QUQEMATH_API void EvaluateWeights(TrainingContext* c, double* weights, int nWeights,
  double* output, double* error, double* gradient);

QUQEMATH_API void DestroyTrainingContext(void* context);

}

extern "C" {

QUQEMATH_API void* CreatePropagationContext(
	LayerSpec* layerSpecs, int nLayers,
	int nInputs,
	double* weights, int nWeights);

QUQEMATH_API void PropagateInput(Frame* frame, double* input, double* output);

QUQEMATH_API void DestroyPropagationContext(void* context);

}

extern "C" {

QUQEMATH_API void* CreateOrthoContext(int basisDimension, int maxBasisCount);
QUQEMATH_API void Orthogonalize(OrthoContext* c, double* p, int numBases, double* orthonormalBases);
QUQEMATH_API void DestroyOrthoContext(void* context);

}

extern "C" int GetWeightCount(LayerSpec* layerSpecs, int nLayers,	int nInputs);

Frame** LayersToFrames(Layer** protoLayers, int nLayers, int nSamples);
void DeleteFrames(Frame** frames, int nSamples);
void DeleteLayers(Layer** layers, int nLayers);

void Propagate(double* input, int inputStride, int numLayers, Layer** currLayers, Layer** prevLayers);
void PropagateLayer(double* input, int inputStride, Layer* layer, Vector* recurrentInput);
void ApplyActivationFunction(Vector* a, ActivationFunc f);
Layer** SpecsToLayers(int numInputs, LayerSpec* specs, int numLayers);
Vector* MakeTimeZeroRecurrentInput(int size);

void SetWeights(Layer** layers, int numLayers, double* weights, int nWeights);
double* SetVectorWeights(Vector* v, double* weights);
double* SetMatrixWeights(Matrix* m, double* weights);

int GetWeights(Layer** layers, int numLayers, double* weights, int nWeights);
double* GetVectorWeights(Vector* v, double* weights);
double* GetMatrixWeights(Matrix* m, double* weights);

double Linear(double x);
double LinearPrime(double x);
double LogisticSigmoidPrime(double x);

inline double Linear(double x)
{
  return x;
}

inline double LinearPrime(double x)
{
  return 1;
}

//inline double LogisticSigmoid(double x)
//{
//  return 1 / (1 + exp(-x));
//}

#define LogisticSigmoid(x)    (1 / (1 + exp(-(x))))

inline double LogisticSigmoidPrime(double x)
{
  double v = LogisticSigmoid(x);
  return v * (1 - v);
}