using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quqe
{
  public class RnnTrainRec : Expert
  {
    public Vec InitialWeights { get; private set; }
    public MRnnSpec RnnSpec { get; private set; }
    public double[] CostHistory { get; private set; }

    public RnnTrainRec(Database db, ObjectId mixtureId, Chromosome chromosome, Vec initialWeights, MRnnSpec rnnSpec, IEnumerable<double> costHistory)
      : base(db, mixtureId, chromosome)
    {
      InitialWeights = initialWeights;
      RnnSpec = rnnSpec;
      CostHistory = costHistory.ToArray();
      Database.Store(this);
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

  public class MRnnSpec
  {
    public int NumInputs { get; private set; }
    public MLayerSpec[] Layers { get; private set; }

    public MRnnSpec(int numInputs, IEnumerable<MLayerSpec> layers)
    {
      NumInputs = numInputs;
      Layers = layers.ToArray();
    }
  }

  public class MLayerSpec
  {
    public int NodeCount { get; private set; }
    public bool IsRecurrent { get; private set; }
    [BsonRepresentation(BsonType.String)]
    public ActivationType ActivationType { get; private set; }

    public MLayerSpec(int nodeCount, bool isRecurrent, ActivationType activationType)
    {
      NodeCount = nodeCount;
      IsRecurrent = isRecurrent;
      ActivationType = activationType;
    }
  }
}
