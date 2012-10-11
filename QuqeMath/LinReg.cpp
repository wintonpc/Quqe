#include "stdafx.h"
#include "LinReg.h"
#include <exception>

Vector::Vector(int count)
{
  Count = count;
  Data = (double*)_aligned_malloc(count * sizeof(double), 64);
  if (Data == NULL)
    throw std::bad_alloc();
}

Vector::Vector(int count, double* data)
{
  Count = count;
  Data = (double*)_aligned_malloc(count * sizeof(double), 64);
  if (Data == NULL)
    throw std::bad_alloc();
  memcpy(Data, data, count * sizeof(double));
}

Vector::Vector(const Vector &v)
{
  Count = v.Count;
  Data = (double*)_aligned_malloc(Count * sizeof(double), 64);
  if (Data == NULL)
    throw std::bad_alloc();
  memcpy(Data, v.Data, Count * sizeof(double));
}

void Vector::Set(Vector* v)
{
  cblas_dcopy(v->Count, v->Data, 1, Data, 1);
}

void Vector::Set(double* data, int stride, int count)
{
  cblas_dcopy(count, data, stride, Data, 1);
}

void Vector::Zero()
{
  memset(Data, 0, Count * sizeof(double));
}

Vector::~Vector()
{
  if (Data != NULL)
    _aligned_free(Data);
}

Matrix::Matrix(int nRows, int nCols)
{
  RowCount = nRows;
  ColumnCount = nCols;
  DataLen = nRows * nCols;
  Data = (double*)_aligned_malloc(DataLen * sizeof(double), 64);
  if (Data == NULL)
    throw std::bad_alloc();
}

Matrix::Matrix(int nRows, int nCols, double* data)
{
  RowCount = nRows;
  ColumnCount = nCols;
  DataLen = nRows * nCols;
  Data = (double*)_aligned_malloc(DataLen * sizeof(double), 64);
  if (Data == NULL)
    throw std::bad_alloc();
  memcpy(Data, data, DataLen * sizeof(double));
}

Matrix::Matrix(const Matrix &m)
{
  RowCount = m.RowCount;
  ColumnCount = m.ColumnCount;
  DataLen = RowCount * ColumnCount;
  Data = (double*)_aligned_malloc(DataLen * sizeof(double), 64);
  if (Data == NULL)
    throw std::bad_alloc();
  memcpy(Data, m.Data, DataLen * sizeof(double));
}

void Matrix::Zero()
{
  memset(Data, 0, DataLen * sizeof(double));
}

Matrix::~Matrix()
{
  if (Data != NULL)
    _aligned_free(Data);
}
