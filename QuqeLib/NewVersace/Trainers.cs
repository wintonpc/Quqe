using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using PCW;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public interface IGenTrainer
  {
    void Train(TrainingSeed seed, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress);
  }

  public class TrainProgress
  {
    public readonly int Completed;
    public readonly int Total;

    public TrainProgress(int completed, int total)
    {
      Completed = completed;
      Total = total;
    }
  }

  public class TrainingSeed
  {
    public readonly Mat Input;
    public readonly Vec Output;
    public readonly int DatabaseAInputLength;

    public TrainingSeed(Mat input, Vec output, int databaseAInputLength)
    {
      Input = input;
      Output = output;
      DatabaseAInputLength = databaseAInputLength;
    }
  }

  public class LocalTrainer : IGenTrainer
  {
    public void Train(TrainingSeed seed, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      var total = population.Count() * population.First().Chromosomes.Length;
      var numTrained = 0;
      foreach (var mi in population)
        foreach (var chrom in mi.Chromosomes)
        {
          TrainerCommon.Train(gen.Database, mi.MixtureId, seed, chrom);
          numTrained++;
          progress(new TrainProgress(numTrained, total));
        }
    }
  }

  public class LocalParallelTrainer : IGenTrainer
  {
    public void Train(TrainingSeed seed, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      var total = population.Count() * population.First().Chromosomes.Length;
      var numTrained = 0;
      var q = population.SelectMany(mixture => mixture.Chromosomes.Select(chrom => new { mixture, chrom })).ToList();

      Parallel.ForEach(q, GetParallelOptions(), z => {
        TrainerCommon.Train(gen.Database, z.mixture.MixtureId, seed, z.chrom);
        numTrained++;
        progress(new TrainProgress(numTrained, total));
      });
    }

    static ParallelOptions GetParallelOptions() { return new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }; }
  }

  public class ExpertSeed
  {
    public readonly Mat Input;
    public readonly Vec Output;
    public readonly Chromosome Chromosome;

    public ExpertSeed(Mat input, Vec output, Chromosome chromosome)
    {
      Input = input;
      Output = output;
      Chromosome = chromosome;
    }
  }

  static class TrainerCommon
  {
    public static void Train(Database db, ObjectId mixtureId, TrainingSeed tSeed, Chromosome chrom)
    {
      var trimmed = TrimToWindow(tSeed, chrom);
      var data = ExpertPreprocessing.PrepareData(trimmed, chrom);
      var eSeed = new ExpertSeed(data.Item1, data.Item2, chrom);

      switch (chrom.NetworkType)
      {
        case NetworkType.Rnn:
          var rnnInfo = Training.TrainRnn(eSeed);
          new RnnTrainRec(db, mixtureId, chrom, rnnInfo.InitialWeights, rnnInfo.RnnSpec, rnnInfo.CostHistory);
          break;
        case NetworkType.Rbf:
          var rbfInfo = Training.TrainRbf(eSeed);
          new RbfTrainRec(db, mixtureId, chrom, rbfInfo.Bases, rbfInfo.OutputBias, rbfInfo.Spread, rbfInfo.IsDegenerate);
          break;
        default:
          throw new Exception("Unexpected network type: " + eSeed.Chromosome.NetworkType);
      }
    }

    static TrainingSeed TrimToWindow(TrainingSeed tSeed, Chromosome chrom)
    {
      return new TrainingSeed(TrimInputToWindow(tSeed.Input, chrom),
                              TrimOutputToWindow(tSeed.Output, chrom),
                              tSeed.DatabaseAInputLength);
    }

    static Vec TrimOutputToWindow(Vec output, Chromosome chrom)
    {
      var w = GetDataWindowOffsetAndSize(output.Count, chrom);
      return output.SubVector(w.Item1, w.Item2);
    }

    static Mat TrimInputToWindow(Mat inputs, Chromosome chrom)
    {
      var w = GetDataWindowOffsetAndSize(inputs.ColumnCount, chrom);
      return inputs.Columns().Skip(w.Item1).Take(w.Item2).ColumnsToMatrix();
    }

    static Tuple2<int> GetDataWindowOffsetAndSize(int count, Chromosome chrom)
    {
      int offset = MathEx.Clamp(0, count - 1, (int)(chrom.TrainingOffsetPct * count));
      int size = MathEx.Clamp(1, count - offset, (int)(chrom.TrainingSizePct * count));
      return Tuple2.Create(offset, size);
    }
  }
}