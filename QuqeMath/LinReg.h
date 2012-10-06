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
  void Vector::Set(Vector* v);
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
  void Matrix::SetDec(int i, int j, double v);
  void GetColumn(int j, Vector* dest);
  ~Matrix();
};

//void GEMV(double alpha, Matrix* a, Vector* x, double beta, Vector* y);
//double Dot(Vector* a, Vector* b);

inline void Matrix::GetColumn(int j, Vector* dest)
{
  int vCount = RowCount;
  assert(vCount == dest->Count);
  double* vData = dest->Data;
  for (int i = 0, x = j; i < vCount; i++, x += ColumnCount)
    vData[i] = Data[x];
}

inline void Matrix::Set(int i, int j, double v)
{
  Data[i * ColumnCount + j] = v;
}

inline void Matrix::SetDec(int i, int j, double v)
{
  Data[i * ColumnCount + j] -= v;
}

inline void GEMV(double alpha, Matrix* a, Vector* x, double beta, Vector* y)
{
  // TODO: implement
}

inline double Dot(Vector* a, Vector* b)
{
  // TODO: implement
  return 0;
}

#endif