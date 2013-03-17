#ifndef LINREG_H
#define LINREG_H

typedef int blasint; // hack for cblas.h
#define _Complex // hack for cblas.h
#include "cblas.h"

class Vector
{
public:
  int Count;
  double* Data;

  Vector(const Vector &v);
  Vector(int count);
  Vector(int count, double* data);
  void Vector::Set(Vector* v);
  void Vector::Set(double* data, int stride, int count);
  void Zero();
  ~Vector();
};

class Matrix
{
public:
  int RowCount;
  int ColumnCount;
  double* Data;
  int DataLen;
  
  Matrix(const Matrix &m);
  Matrix(int nRows, int nCols);
  Matrix(int nRows, int nCols, double* data);
  void Zero();
  ~Matrix();
};

#define GetRowPtr(m,i)    ((m)->Data + (i) * (m)->ColumnCount)

#define GetColumnPtr(m,j)    ((m)->Data + (j))

inline void GEMV(double alpha, Matrix* a, Vector* x, double beta, Vector* y)
{
  int columnCount = a->ColumnCount;
  cblas_dgemv(CblasRowMajor, CblasNoTrans, a->RowCount, columnCount,
    alpha, a->Data, columnCount, x->Data, 1, beta, y->Data, 1);
}

inline void GEMV(double alpha, Matrix* a, double* x, int xStride, double beta, Vector* y)
{
  int columnCount = a->ColumnCount;
  cblas_dgemv(CblasRowMajor, CblasNoTrans, a->RowCount, columnCount,
    alpha, a->Data, columnCount, x, xStride, beta, y->Data, 1);
}

inline void GER(double alpha, double* x, double* y, Matrix* a)
{
  int columnCount = a->ColumnCount;
  cblas_dger(CblasRowMajor, a->RowCount, columnCount, alpha, x, 1, y, 1, a->Data, columnCount);
}

inline double Dot(Vector* a, Vector* b)
{
  return cblas_ddot(a->Count, a->Data, 1, b->Data, 1);
}

#define DotColumn(a,column,b)   (cblas_ddot((b)->Count, (a)->Data + (column), (a)->ColumnCount, (b)->Data, 1))

#define AXPY(alpha,x,n,y)    (cblas_daxpy((n), (alpha), (x)->Data, 1, (y), 1))
#define AXPY2(alpha,xData,n,y)    (cblas_daxpy((n), (alpha), (xData), 1, (y), 1))

#endif