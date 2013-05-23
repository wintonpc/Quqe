using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quqe
{
  public class RnnTrainRec : Expert
  {
    [BsonSerializer(typeof(VectorSerializer))]
    public Vec InitialWeights { get; private set; }
    public MRnnSpec RnnSpec { get; private set; }
    public double[] CostHistory { get; private set; }

    public RnnTrainRec(Database db, ObjectId mixtureId, Chromosome chromosome,
      Vec initialWeights, MRnnSpec rnnSpec, IEnumerable<double> costHistory)
      : base(db, mixtureId, chromosome)
    {
      InitialWeights = initialWeights;
      RnnSpec = rnnSpec;
      CostHistory = costHistory.ToArray();
      Database.Store(this);
    }
  }

  public class MRnnSpec
  {
    public int NumInputs { get; private set; }
    public MLayerSpec[] Layers { get; private set; }
    [BsonSerializer(typeof(VectorSerializer))]
    public Vec Weights { get; private set; }

    public MRnnSpec(int numInputs, IEnumerable<MLayerSpec> layers, Vec weights)
    {
      NumInputs = numInputs;
      Layers = layers.ToArray();
      Weights = weights;
    }

    public static MRnnSpec FromRnnSpec(RNNSpec s)
    {
      return new MRnnSpec(s.NumInputs, s.Layers.Select(sl => new MLayerSpec(sl.NodeCount, sl.IsRecurrent, sl.ActivationType)), s.Weights);
    }

    public RNNSpec ToRnnSpec()
    {
      return new RNNSpec(this.NumInputs, this.Layers.Select(l => new LayerSpec(l.NodeCount, l.IsRecurrent, l.ActivationType)), this.Weights);
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
