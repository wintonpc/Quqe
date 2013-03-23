using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe
{
  public interface IGenTrainer
  {
    Mixture[] Train(Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress);
  }

  public class TrainProgress
  {
    public readonly int Completed;
    public readonly int Total;
  }

  public class LocalTrainer : IGenTrainer
  {
    public Mixture[] Train(Generation gen, IEnumerable<MixtureInfo> population, Action<TrainProgress> progress)
    {
      throw new NotImplementedException();
    }
  }
}
