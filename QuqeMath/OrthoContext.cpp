#include "stdafx.h"
#include <stdio.h>
#include "QuqeMath.h"
#include "LinReg.h"

OrthoContext::OrthoContext(int basisDimension, int maxBasisCount)
{
	Pv = new Vector(basisDimension);
	Bases = new Matrix(maxBasisCount, basisDimension);
	Dp = new Vector(maxBasisCount);
}

OrthoContext::~OrthoContext()
{
	delete Pv;
	delete Bases;
	delete Dp;
}

QUQEMATH_API void* CreateOrthoContext(int basisDimension, int maxBasisCount)
{
	return new OrthoContext(basisDimension, maxBasisCount);
}

QUQEMATH_API void DestroyOrthoContext(void* context)
{
	delete ((OrthoContext*)context);
}

void Orthogonalize(OrthoContext* c, double* p, int numBases, double* orthonormalBases)
{
	memcpy(c->Pv->Data, p, c->Pv->Count * sizeof(double));
	memcpy(c->Bases->Data, orthonormalBases, numBases * c->Pv->Count * sizeof(double));
	c->Bases->RowCount = numBases;
	c->Dp->Count = numBases;

	int basisLen = c->Pv->Count;
	GEMV(1, c->Bases, c->Pv, 0, c->Dp);
	for (int i = 0, offset = 0; i < numBases; i++, offset += basisLen)
		AXPY2(-1 * c->Dp->Data[i], c->Bases->Data + offset, basisLen, c->Pv->Data);
	double mag = cblas_dnrm2(basisLen, c->Pv->Data, 1);
	cblas_dscal(basisLen, 1.0 / mag, c->Pv->Data, 1);
	memcpy(p, c->Pv->Data, basisLen * sizeof(double));
}