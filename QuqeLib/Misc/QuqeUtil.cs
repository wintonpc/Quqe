using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra.Double;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class QuqeUtil
  {
    public static Random Random = new Random();

    public static Vec MakeRandomVector(int size, double min, double max)
    {
      return new DenseVector(ContinuousUniform.Samples(Random, min, max).Take(size).ToArray());
    }
  }
}
