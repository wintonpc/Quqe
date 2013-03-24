using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Quqe
{
  public interface IGenTrainer
  {
    void Train(Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress);
  }

  public class TrainProgress
  {
    public readonly int Completed;
    public readonly int Total;
  }

  public class FakeTrainer : IGenTrainer
  {
    public void Train(Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      foreach (var mi in population)
        foreach (var chrom in mi.Chromosomes)
          Train(gen.Database, mi.MixtureId, chrom);
    }

    void Train(Database db, ObjectId mixtureId, Chromosome chrom)
    {
      if (chrom.NetworkType == NetworkType.Rnn)
        new RnnTrainRec(db, mixtureId, chrom);
      else
        new RbfTrainRec(db, mixtureId, chrom);
    }
  }
}
