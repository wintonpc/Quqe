using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using Quqe.NewVersace;

namespace Quqe
{
  public class RbfTrainRec : Expert
  {
    public MRadialBasis[] Bases { get; private set; }
    public double OutputBias { get; private set; }
    public double Spread { get; private set; }
    public bool IsDegenerate { get; private set; }

    public RbfTrainRec(Database db, ObjectId mixtureId, Chromosome chromosome,
      IEnumerable<MRadialBasis> bases, double outputBias, double spread, bool isDegenerate)
      : base(db, mixtureId, chromosome)
    {
      Bases = bases.ToArray();
      OutputBias = outputBias;
      Spread = spread;
      IsDegenerate = isDegenerate;

      Database.Store(this);
    }
  }

  public class MRadialBasis
  {
    [BsonSerializer(typeof(VectorSerializer))]
    public Vec Center { get; private set; }
    public double Weight { get; private set; }

    public MRadialBasis(Vec center, double weight)
    {
      Center = center;
      Weight = weight;
    }

    public static MRadialBasis FromRadialBasis(RadialBasis rb)
    {
      return new MRadialBasis(rb.Center, rb.Weight);
    }

    public RadialBasis ToRadialBasis()
    {
      return new RadialBasis(this.Center, this.Weight);
    }
  }
}
