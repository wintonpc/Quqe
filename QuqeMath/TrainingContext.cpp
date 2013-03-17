#include "stdafx.h"
#include <stdio.h>
#include "QuqeMath.h"
#include "LinReg.h"

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
}

TrainingContext::~TrainingContext()
{
	DeleteFrames(Frames, TrainingInput->ColumnCount);
	delete [] LayerSpecs;
	delete TrainingInput;
	delete TrainingOutput;
}

QUQEMATH_API void* CreateTrainingContext(
	LayerSpec* layerSpecs, int nLayers,
	double* trainingData, double* outputData,
	int nInputs, int nSamples)
{
	Layer** protoLayers = SpecsToLayers(nInputs, layerSpecs, nLayers);
	Frame** frames = LayersToFrames(protoLayers, nLayers, nSamples);
	DeleteLayers(protoLayers, nLayers, false);
	TrainingContext* context = new TrainingContext(Matrix(nInputs, nSamples, trainingData), Vector(nSamples, outputData), nSamples, frames, nLayers, layerSpecs);
	return context;
}

QUQEMATH_API void DestroyTrainingContext(void* context)
{
	delete ((TrainingContext*)context);
}