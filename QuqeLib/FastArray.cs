using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe
{
  public class FastArray2
  {
    public readonly int Length0;
    public readonly int Length1;
    double[] A;

    public FastArray2(int len0, int len1)
    {
      A = new double[len0 * len1];
      Length0 = len0;
      Length1 = len1;
    }

    public double this[int i0, int i1]
    {
      get { return A[i0 * Length1 + i1]; }
      set { A[i0 * Length1 + i1] = value; }
    }

    public double[] GetUnderlyingArray()
    {
      return A;
    }
  }

  public class FastArray3
  {
    public readonly int Length0;
    public readonly int Length1;
    public readonly int Length2;
    double[] A;

    public FastArray3(int len0, int len1, int len2)
    {
      A = new double[len0 * len1 * len2];
      Length0 = len0;
      Length1 = len1;
      Length2 = len2;
    }

    public double this[int i0, int i1, int i2]
    {
      get { return A[i0 * Length1 * Length2 + i1 * Length2 + i2]; }
      set { A[i0 * Length1 * Length2 + i1 * Length2 + i2] = value; }
    }

    public double[] GetUnderlyingArray()
    {
      return A;
    }
  }
}
