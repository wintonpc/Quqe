#ifndef LINREG_H
#define LINREG_H

class Vector
{
public:
  int Count;
  double* Data;

  Vector();
  Vector(const Vector &v);
  Vector(int count);
  Vector(int count, double* data);
  ~Vector();
};

class Matrix
{
public:
  int RowCount;
  int ColumnCount;
  double* Data;
  int DataLen;
  
  Matrix();
  Matrix(const Matrix &m);
  Matrix(int nRows, int nCols);
  Matrix(int nRows, int nCols, double* data);
  void Set(int i, int j, double v);
  Vector Column(int j);
  ~Matrix();

  static Vector* MultAndAdd(Matrix* a, Vector* x, Vector* y);
};

#endif