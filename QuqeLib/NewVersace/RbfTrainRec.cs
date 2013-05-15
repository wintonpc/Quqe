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
    public readonly MRadialBasis[] Bases;
    public readonly double OutputBias;
    public readonly double Spread;
    public readonly bool IsDegenerate;

    public RbfTrainRec(Database db, ObjectId mixtureId, Chromosome chromosome)
      : base(db, mixtureId, chromosome)
    {
      Database.Store(this);
    }
  }

  public class MRadialBasis
  {
    [BsonSerializer(typeof(VectorSerializer))]
    public readonly Vec Center;
    public readonly double Weight;

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
