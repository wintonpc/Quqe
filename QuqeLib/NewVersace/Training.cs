using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Double;
using MongoDB.Bson;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class Training
  {
    public static RnnTrainRecInfo TrainRnn(ExpertSeed seed)
    {
      Func<int, LayerSpec> logisticSigmoidRecurrent = nodeCount =>
        new LayerSpec(nodeCount, true, ActivationType.LogisticSigmoid);

      var layers = new List<LayerSpec> {
        logisticSigmoidRecurrent(seed.Chromosome.RnnLayer1NodeCount),
        logisticSigmoidRecurrent(seed.Chromosome.RnnLayer2NodeCount),
        new LayerSpec(1, false, ActivationType.Linear)
      };
      var epochMax = seed.Chromosome.RnnTrainingEpochs;

      var numInputs = seed.Input.RowCount;
      var rnnWeightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = RNN.MakeRandomWeights(rnnWeightCount);
      var trainResult = RNN.TrainSCG(layers, initialWeights, epochMax, seed.Input, seed.Output);

      return new RnnTrainRecInfo(initialWeights, MRnnSpec.FromRnnSpec(trainResult.RNNSpec), trainResult.CostHistory);
    }

    public static RbfTrainRecInfo TrainRbf(ExpertSeed seed)
    {
      var rbf = RBFNet.Train(seed.Input, seed.Output, seed.Chromosome.RbfNetTolerance, seed.Chromosome.RbfGaussianSpread);
      return new RbfTrainRecInfo(rbf.Bases.Select(MRadialBasis.FromRadialBasis), rbf.OutputBias, rbf.Spread, rbf.IsDegenerate);
    }
  }
}
