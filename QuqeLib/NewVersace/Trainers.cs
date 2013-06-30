using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using Quqe.NewVersace;
using Quqe.Rabbit;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public interface IGenTrainer
  {
    void Train(DataSet data, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress);
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

  public class DataSet
  {
    public readonly Mat Input;
    public readonly Vec Output;
    public readonly int DatabaseAInputLength;

    public DataSet(Mat input, Vec output, int databaseAInputLength)
    {
      Input = input;
      Output = output;
      DatabaseAInputLength = databaseAInputLength;
    }
  }

  public class LocalTrainer : IGenTrainer
  {
    public void Train(DataSet data, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      var total = population.Count() * population.First().Chromosomes.Length;
      var numTrained = 0;
      foreach (var mi in population)
        foreach (var chrom in mi.Chromosomes)
        {
          TrainerCommon.Train(gen.Database, mi.MixtureId, data, chrom);
          numTrained++;
          progress(new TrainProgress(numTrained, total));
        }
    }
  }

  public class LocalParallelTrainer : IGenTrainer
  {
    public void Train(DataSet data, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      var total = population.Count() * population.First().Chromosomes.Length;
      var numTrained = 0;
      var q = population.SelectMany(mixture => mixture.Chromosomes.Select(chrom => new { mixture, chrom })).ToList();

      Parallel.ForEach(q, GetParallelOptions(), z => {
        TrainerCommon.Train(gen.Database, z.mixture.MixtureId, data, z.chrom);
        numTrained++;
        progress(new TrainProgress(numTrained, total));
      });
    }

    static ParallelOptions GetParallelOptions()
    {
      return new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
    }
  }

  public class DistributedTrainer : IGenTrainer
  {
    public void Train(DataSet data, Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      var rabbitHost = ConfigurationManager.AppSettings["RabbitHost"];
      using (var trainRequests = new WorkQueueProducer(new WorkQueueInfo(rabbitHost, "TrainRequests", false)))
      using (var trainNotifications = new Broadcaster(new BroadcastInfo(rabbitHost, "TrainNotifications")))
      {
        List<TrainRequest> outstanding = (from mixture in population
                                          from chrom in mixture.Chromosomes
                                          select new TrainRequest(mixture.MixtureId.ToString(), chrom)).ToList();

        int total = outstanding.Count;
        
        foreach (var msg in outstanding.Where(x => x.Chromosome.NetworkType == NetworkType.Rnn))
          trainRequests.Send(msg);
        foreach (var msg in outstanding.Where(x => x.Chromosome.NetworkType == NetworkType.Rbf))
          trainRequests.Send(msg);

        trainNotifications.On<TrainNotification>(notification => {
          outstanding.RemoveAll(x => x.MixtureId == notification.OriginalRequest.MixtureId &&
                                     x.Chromosome.OrderInMixture == notification.OriginalRequest.Chromosome.OrderInMixture);
          progress(new TrainProgress(total - outstanding.Count, total));
        });

        Waiter.Wait(() => !outstanding.Any());
      }
    }
  }

  public delegate RnnTrainRec MakeRnnTrainRecFunc(Vec initialWeights, MRnnSpec rnnSpec, IEnumerable<double> costHistory);

  public delegate RbfTrainRec MakeRbfTrainRecFunc(IEnumerable<MRadialBasis> bases, double outputBias, double spread, bool isDegenerate);

  public static class TrainerCommon
  {
    public static void Train(Database db, ObjectId mixtureId, DataSet trainingSet, Chromosome chrom)
    {
      var trimmed = TrimToWindow(trainingSet, chrom);
      var tailoredData = DataTailoring.TailorInputs(trimmed.Input, trimmed.DatabaseAInputLength, chrom);

      switch (chrom.NetworkType)
      {
        case NetworkType.Rnn:
          Training.TrainRnn(tailoredData, trimmed.Output, chrom, (a, b, c) => new RnnTrainRec(db, mixtureId, chrom, a, b, c));
          break;
        case NetworkType.Rbf:
          Training.TrainRbf(tailoredData, trimmed.Output, chrom, (a, b, c, d) => new RbfTrainRec(db, mixtureId, chrom, a, b, c, d));
          break;
        default:
          throw new Exception("Unexpected network type: " + chrom.NetworkType);
      }
    }

    static DataSet TrimToWindow(DataSet data, Chromosome chrom)
    {
      return new DataSet(TrimInputToWindow(data.Input, chrom),
                         TrimOutputToWindow(data.Output, chrom),
                         data.DatabaseAInputLength);
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