using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public class RbfTrainRec : Expert
  {
    public readonly MRadialBasis[] Bases;
    public readonly double OutputBias;
    public readonly double Spread;
    public readonly bool IsDegenerate;

    public RbfTrainRec(Mixture m) : base(m.Database) { }
  }

  public class MRadialBasis
  {
    public readonly Vec Center;
    public readonly double Weight;
  }
}
