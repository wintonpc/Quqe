#include "stdafx.h"
#include "LinReg.h"

Vector::Vector()
{
  Data = NULL;
}

Vector::Vector(int count)
{
  Count = count;
  Data = (double*)mkl_malloc(count * sizeof(double), 64);
}

Vector::Vector(int count, double* data)
{
  Count = count;
  Data = (double*)mkl_malloc(count * sizeof(double), 64);
  memcpy(Data, data, count * sizeof(double));
}

Vector::Vector(const Vector &v)
{
  Count = v.Count;
  Data = (double*)mkl_malloc(Count * sizeof(double), 64);
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
    mkl_free(Data);
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
  Data = (double*)mkl_malloc(DataLen * sizeof(double), 64);
}

Matrix::Matrix(int nRows, int nCols, double* data)
{
  RowCount = nRows;
  ColumnCount = nCols;
  DataLen = nRows * nCols;
  Data = (double*)mkl_malloc(DataLen * sizeof(double), 64);
  memcpy(Data, data, DataLen * sizeof(double));
}

Matrix::Matrix(const Matrix &m)
{
  RowCount = m.RowCount;
  ColumnCount = m.ColumnCount;
  DataLen = RowCount * ColumnCount;
  Data = (double*)mkl_malloc(DataLen * sizeof(double), 64);
  memcpy(Data, m.Data, DataLen * sizeof(double));
}

Matrix::~Matrix()
{
  if (Data != NULL)
    mkl_free(Data);
}
