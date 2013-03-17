#include "stdafx.h"
#include <stdio.h>
#include "QuqeMath.h"
#include "LinReg.h"

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

