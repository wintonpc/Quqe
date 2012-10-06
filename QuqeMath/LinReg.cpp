#include "stdafx.h"
#include "LinReg.h"

Vector::Vector()
{
  Data = NULL;
}

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

Vector::Vector(const Vector &v)
{
  Count = v.Count;
  Data = new double[Count];
  memcpy(Data, v.Data, Count * sizeof(double));
}

void Vector::Set(Vector* v)
{
  Count = v->Count;
  memcpy(Data, v->Data, Count * sizeof(double));
}

Vector::~Vector()
{
  if (Data != NULL)
    delete [] Data;
}

Matrix::Matrix()
{
  Data = NULL;
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

Matrix::Matrix(const Matrix &m)
{
  RowCount = m.RowCount;
  ColumnCount = m.ColumnCount;
  DataLen = RowCount * ColumnCount;
  Data = new double[DataLen];
  memcpy(Data, m.Data, DataLen * sizeof(double));
}

inline void Matrix::Set(int i, int j, double v)
{
  Data[i * ColumnCount + j] = v;
}

void Matrix::GetColumn(int j, Vector* dest)
{
  int vCount = RowCount;
  assert(vCount == dest->Count);
  double* vData = dest->Data;
  for (int i = 0, x = j; i < vCount; i++, x += ColumnCount)
    vData[i] = Data[x];
}

void Matrix::GEMV(double alpha, Matrix* a, Vector* x, double beta, Vector* y)
{
}

Matrix::~Matrix()
{
  if (Data != NULL)
    delete [] Data;
}
