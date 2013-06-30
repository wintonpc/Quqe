using System;
using System.Collections.Generic;
using System.Linq;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class Training
  {
    public static RnnTrainRec TrainRnn(Mat input, Vec output, Chromosome chrom, MakeRnnTrainRecFunc makeResult)
    {
      Func<int, LayerSpec> logisticSigmoidRecurrent = nodeCount =>
                                                      new LayerSpec(nodeCount, true, ActivationType.LogisticSigmoid);

      var layers = new List<LayerSpec> {
        logisticSigmoidRecurrent(chrom.RnnLayer1NodeCount),
        logisticSigmoidRecurrent(chrom.RnnLayer2NodeCount),
        new LayerSpec(1, false, ActivationType.Linear)
      };
      var epochMax = chrom.RnnTrainingEpochs;

      var numInputs = input.RowCount;
      var rnnWeightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = RNN.MakeRandomWeights(rnnWeightCount);
      var trainResult = RNN.TrainSCG(layers, initialWeights, epochMax, input, output);

      return makeResult(initialWeights, MRnnSpec.FromRnnSpec(trainResult.RNNSpec), trainResult.CostHistory);
    }

    public static RbfTrainRec TrainRbf(Mat input, Vec output, Chromosome chrom, MakeRbfTrainRecFunc makeResult)
    {
      using (var rbf = RBFNet.Train(input, output, chrom.RbfNetTolerance, chrom.RbfGaussianSpread))
        return makeResult(rbf.Bases.Select(MRadialBasis.FromRadialBasis), rbf.OutputBias, rbf.Spread, rbf.IsDegenerate);
    }
  }
}