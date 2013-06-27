using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Internal;
using Quqe;
using Quqe.NewVersace;
using Quqe.Rabbit;

namespace HostLib
{
  static class Master
  {
    public static void Run(Action onGenerationComplete)
    {
      using (var masterRequests = new SyncWorkQueueConsumer(new WorkQueueInfo(ConfigurationManager.AppSettings["RabbitHost"], "MasterRequests", false)))
      using (var versaceBroadcast = new Broadcaster(new BroadcastInfo(ConfigurationManager.AppSettings["RabbitHost"], "VersaceMsgs")))
      {
        var msg = masterRequests.Receive(3000);
        if (msg is ReceiveTimedOut)
        {
          Console.WriteLine("Not the master :( exiting");
          return;
        }
        var req = (MasterRequest)msg;

        Console.WriteLine("I AM THE MASTER!");

        var db = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
        var protoRun = db.QueryOne<ProtoRun>(x => x.Name == req.ProtoRunName);

        var dataSets = DataPreprocessing.MakeTrainingAndValidationSets(req.Symbol, req.StartDate, req.EndDate, req.ValidationPct, GetSignalFunc(req.SignalType));

        var run = Functions.Evolve(protoRun, new DistributedTrainer(), dataSets.Item1, dataSets.Item2, gen => {
          versaceBroadcast.Send(new MasterUpdate(gen.Id, gen.Order, gen.Evaluated.Fitness));
          onGenerationComplete();
        });

        versaceBroadcast.Send(new MasterResult(run.Id));
      }
    }

    public static Func<DataSeries<Bar>, double> GetSignalFunc(SignalType sigType)
    {
      if (sigType == SignalType.NextClose)
        return Signals.NextClose;
      throw new NotImplementedException("Unexpected signal type: " + sigType);
    }
  }
}