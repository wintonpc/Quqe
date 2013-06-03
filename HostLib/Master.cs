using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using MongoDB.Driver;
using MongoDB.Driver.Internal;
using Quqe;
using Quqe.NewVersace;

namespace HostLib
{
  class Master
  {
    public Master(Action onGenerationComplete)
    {
      using (var rabbit = new Rabbit(ConfigurationManager.AppSettings["RabbitHost"]))
      {
        var req = rabbit.TryGetMasterRequest();
        if (req == null)
          return;

        Console.WriteLine("I AM THE MASTER!");

        var db = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
        var protoRun = db.QueryOne<ProtoRun>(x => x.Name == req.ProtoRunName);

        var dataSets = DataPreprocessing.MakeTrainingAndValidationSets(req.Symbol, req.StartDate, req.EndDate, req.ValidationPct, GetSignalFunc(req.SignalType));

        var run = Functions.Evolve(protoRun, new DistrubutedTrainer(), dataSets.Item1, dataSets.Item2, gen => {
          rabbit.SendMasterUpdate(new MasterUpdate(gen.Id, gen.Order, gen.Evaluated.Fitness));
          onGenerationComplete();
        });

        rabbit.SendMasterResult(new MasterResult(run.Id));
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