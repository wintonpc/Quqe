using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quqe.Rabbit;

namespace Quqe.NewVersace
{
  public class TrainRequest : RabbitMessage
  {
    public Chromosome Chromosome { get; private set; }

    public TrainRequest(Chromosome chromosome)
    {
      Chromosome = chromosome;
    }
  }
}
