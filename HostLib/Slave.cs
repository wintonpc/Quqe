using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Quqe.NewVersace;

namespace HostLib
{
  public static class Slave
  {
    public static void Run(Action onIdle)
    {
      using (var rabbit = new Rabbit(ConfigurationManager.AppSettings["RabbitHost"]))
      {
        TrainRequest req;
        while ((req = rabbit.GetTrainRequest()) == null)
          onIdle();
      }
    }
  }
}
