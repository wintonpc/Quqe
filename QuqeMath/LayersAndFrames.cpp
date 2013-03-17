#include "stdafx.h"
#include <stdio.h>
#include "QuqeMath.h"
#include "LinReg.h"

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
		DeleteLayers(Layers, NumLayers, false);
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
		frames[0]->Layers[l]->DeleteWeights();
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