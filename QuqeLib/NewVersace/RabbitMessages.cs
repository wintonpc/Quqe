using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe.NewVersace
{
  public class TrainRequest
  {
    public Chromosome Chromosome { get; private set; }

    public TrainRequest(Chromosome chromosome)
    {
      Chromosome = chromosome;
    }
  }
}
