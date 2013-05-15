using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public class MixtureInfo
  {
    public readonly ObjectId MixtureId;
    public readonly Chromosome[] Chromosomes;
    [BsonIgnore]
    public readonly Mixture[] Parents;

    public MixtureInfo(ObjectId mixtureId, IEnumerable<Chromosome> chromosomes)
    {
      MixtureId = mixtureId;
      Chromosomes = chromosomes.ToArray();
    }

    public MixtureInfo(IEnumerable<Mixture> parents, IEnumerable<Chromosome> chromosomes)
    {
      Parents = parents.ToArray();
      Chromosomes = chromosomes.ToArray();
    }
  }

  public class RnnTrainRecInfo
  {
    public readonly Vec InitialWeights;
    public readonly MRnnSpec RnnSpec;
    public readonly double[] CostHistory;

    public RnnTrainRecInfo(Vec initialWeights, MRnnSpec rnnSpec, IEnumerable<double> costHistory)
    {
      InitialWeights = initialWeights;
      RnnSpec = rnnSpec;
      CostHistory = costHistory.ToArray();
    }
  }

  public class RbfTrainRecInfo
  {
    public readonly MRadialBasis[] Bases;
    public readonly double OutputBias;
    public readonly double Spread;
    public readonly bool IsDegenerate;

    public RbfTrainRecInfo(IEnumerable<MRadialBasis> bases, double outputBias, double spread, bool isDegenerate)
    {
      Bases = bases.ToArray();
      OutputBias = outputBias;
      Spread = spread;
      IsDegenerate = isDegenerate;
    }
  }
}
