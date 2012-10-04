#include "stdafx.h"
#include "LinReg.h"

Vector::Vector(int count)
{
  Count = count;
  Data = new double[count];
}

Vector::Vector(int count, double* data)
{
  Count = count;
  Data = new double[count];
  memcpy(Data, data, count * sizeof(double));
}

Vector* Vector::Copy()
{
  return new Vector(Count, Data);
}

Vector::~Vector()
{
  if (Data != NULL)
    delete [] Data;
}

Matrix::Matrix(int nRows, int nCols)
{
  RowCount = nRows;
  ColumnCount = nCols;
  DataLen = nRows * nCols;
  Data = new double[DataLen];
}

Matrix::Matrix(int nRows, int nCols, double* data)
{
  RowCount = nRows;
  ColumnCount = nCols;
  DataLen = nRows * nCols;
  Data = new double[DataLen];
  memcpy(Data, data, DataLen * sizeof(double));
}

void Matrix::Set(int i, int j, double v)
{
  Data[i * ColumnCount + j] = v;
}

Vector Matrix::Column(int j)
{
  int vCount = RowCount;
  double* vData = new double[vCount];
  for (int i = 0, x = j; i < vCount; i++, x += ColumnCount)
    vData[i] = Data[x];
  return Vector(vCount, vData);
}


Vector* Matrix::MultAndAdd(Matrix* a, Vector* x, Vector* y)
{
  return new Vector(y->Count);
}

Matrix::~Matrix()
{
  if (Data != NULL)
    delete [] Data;
}
