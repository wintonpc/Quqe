using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;

namespace Quqe
{
  public interface ITrainer
  {
    void Train(IEnumerable<VExpert> experts, Action<int> onTrainedOne);
  }

  public class SequentialTrainer : ITrainer
  {
    public void Train(IEnumerable<VExpert> experts, Action<int> onTrainedOne)
    {
      int numTrained = 0;
      foreach (var x in experts)
      {
        x.Train();
        numTrained++;
        if (onTrainedOne != null)
          onTrainedOne(numTrained);
      }
    }

    public void ComputeFitness(IEnumerable<VMixture> mixtures)
    {
      foreach (var x in mixtures)
        x.ComputeFitness();
    }
  }

  public class ParallelTrainer : ITrainer
  {
    ParallelOptions ParallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

    public void Train(IEnumerable<VExpert> experts, Action<int> onTrainedOne = null)
    {
      GCSettings.LatencyMode = GCLatencyMode.Batch;
      object Lock = new object();
      int numTrained = 0;

      Parallel.Invoke(ParallelOptions,
        experts.OrderByDescending(x => x.RelativeComplexity).Select(x => new Action(() => {
          x.Train();
          lock (Lock)
          {
            numTrained++;
            if (onTrainedOne != null)
              onTrainedOne(numTrained);
            if (numTrained % 10 == 0)
              GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
          }
        })).ToArray());

      GC.Collect();
    }

    public void ComputeFitness(IEnumerable<VMixture> mixtures)
    {
      Parallel.Invoke(ParallelOptions, mixtures.Select(x => new Action(() => x.ComputeFitness())).ToArray());
    }
  }
}
