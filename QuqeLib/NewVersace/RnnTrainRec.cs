using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using MongoDB.Bson;

namespace Quqe
{
  public class RnnTrainRec : Expert
  {
    public readonly Vec InitialWeights;
    public readonly MRnnSpec RnnSpec;
    public readonly double[] CostHistory;

    public RnnTrainRec(Database db, ObjectId mixtureId, Chromosome chromosome)
      : base(db, mixtureId, chromosome)
    {
      Database.Store(this);
    }
  }

  public class MRnnSpec
  {
    public readonly int NumInputs;
    public readonly MLayerSpec[] Layers;
  }

  public class MLayerSpec
  {
    public readonly int NodeCount;
    public readonly bool IsRecurrent;
    public readonly ActivationType ActivationType;
  }
}
