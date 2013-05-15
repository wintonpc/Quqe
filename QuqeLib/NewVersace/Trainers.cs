using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using PCW;
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

  public class ExpertSeed : TrainingSeed
  {
    public readonly Chromosome Chromosome;

    public ExpertSeed(Chromosome chrom, Mat input, Vec output, int databaseAInputLength)
      : base(input, output, databaseAInputLength)
    {
      Chromosome = chrom;
    }

    public ExpertSeed(Chromosome chrom, TrainingSeed seed)
      : this(chrom, seed.Input, seed.Output, seed.DatabaseAInputLength) { }
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
          Train(gen.Database, mi.MixtureId, new ExpertSeed(chrom, seed));
          numTrained++;
          progress(new TrainProgress(numTrained, total));
        }
    }

    void Train(Database db, ObjectId mixtureId, ExpertSeed seed)
    {
      switch (seed.Chromosome.NetworkType)
      {
        case NetworkType.Rnn:
          var record = Training.TrainRnn(seed);
          new RnnTrainRec(db, mixtureId, seed.Chromosome, record.InitialWeights, record.RnnSpec, record.CostHistory);
          break;
        case NetworkType.Rbf: Training.TrainRbf(db, mixtureId, seed.Chromosome); break;
        default: throw new Exception("Unexpected network type: " + seed.Chromosome.NetworkType);
      }
    }
  }

  public class FakeTrainer : IGenTrainer
  {
    public void Train(TrainingSeed seed, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      foreach (var mi in population)
        foreach (var chrom in mi.Chromosomes)
          Train(gen.Database, mi.MixtureId, chrom);
    }

    void Train(Database db, ObjectId mixtureId, Chromosome chrom)
    {
      if (chrom.NetworkType == NetworkType.Rnn)
        new RnnTrainRec(db, mixtureId, chrom, null, null, new List<double>());
      else
        new RbfTrainRec(db, mixtureId, chrom);
    }
  }
}
