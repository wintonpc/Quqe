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
      var data = PrepareData(seed);
      var input = data.Item1;
      var output = data.Item2;

      Func<int, LayerSpec> logisticSigmoidRecurrent = nodeCount =>
        new LayerSpec(nodeCount, true, ActivationType.LogisticSigmoid);

      var layers = new List<LayerSpec> {
        logisticSigmoidRecurrent(seed.Chromosome.RnnLayer1NodeCount),
        logisticSigmoidRecurrent(seed.Chromosome.RnnLayer2NodeCount),
        new LayerSpec(1, false, ActivationType.Linear)
      };
      var epochMax = seed.Chromosome.RnnTrainingEpochs;

      var numInputs = input.RowCount;
      var rnnWeightCount = RNN.GetWeightCount(layers, numInputs);
      var initialWeights = RNN.MakeRandomWeights(rnnWeightCount);
      var trainResult = RNN.TrainSCG(layers, initialWeights, epochMax, input, output);

      return new RnnTrainRecInfo(initialWeights, MRnnSpec.FromRnnSpec(trainResult.RNNSpec), trainResult.CostHistory);
    }

    public static Tuple<Mat, Vec> PrepareData(ExpertSeed seed)
    {
      var inputs = PreprocessInputs(seed.Input.Columns(), seed.Chromosome, seed.DatabaseAInputLength);
      var outputs = TrimOutputToWindow(seed.Output, seed.Chromosome);
      return new Tuple<Mat, Vec>(inputs.ColumnsToMatrix(), outputs);
    }

    public static RbfTrainRecInfo TrainRbf(ExpertSeed seed)
    {
      var rbf = RBFNet.Train(seed.Input, seed.Output, seed.Chromosome.RbfNetTolerance, seed.Chromosome.RbfGaussianSpread);
      return new RbfTrainRecInfo(rbf.Bases.Select(MRadialBasis.FromRadialBasis), rbf.OutputBias, rbf.Spread, rbf.IsDegenerate);
    }

    public static Tuple2<int> GetDataWindowOffsetAndSize(int count, Chromosome chrom)
    {
      int offset = MathEx.Clamp(0, count - 1, (int)(chrom.TrainingOffsetPct * count));
      int size = MathEx.Clamp(1, count - offset, (int)(chrom.TrainingSizePct * count));
      return Tuple2.Create(offset, size);
    }

    public static Vec TrimOutputToWindow(Vec output, Chromosome chrom)
    {
      var w = GetDataWindowOffsetAndSize(output.Count, chrom);
      return output.SubVector(w.Item1, w.Item2);
    }

    public static List<Vec> PreprocessInputs(List<Vec> inputs, Chromosome chrom, int databaseAInputLength)
    {
      // training window
      var w = GetDataWindowOffsetAndSize(inputs.Count, chrom);
      inputs = inputs.Skip(w.Item1).Take(w.Item2).ToList();

      // database selection
      if (chrom.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => x.SubVector(0, databaseAInputLength)).ToList();

      // complement coding
      if (chrom.UseComplementCoding)
        inputs = inputs.Select(Preprocessing.ComplementCode).ToList();

      // PCA
      if (chrom.UsePCA)
      {
        var principalComponents = Preprocessing.PrincipleComponents(inputs.ColumnsToMatrix());
        var pcNumber = Math.Min(chrom.PrincipalComponent, principalComponents.ColumnCount - 1);
        inputs = inputs.Select(x => Preprocessing.NthPrincipleComponent(principalComponents, pcNumber, x)).ToList();
      }

      return inputs;
    }
  }
}
