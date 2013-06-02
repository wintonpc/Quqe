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
      var req = Rabbit.TryGetMasterRequest();
      if (req == null)
        return;

      var mongoClient = new MongoClient(ConfigurationManager.AppSettings["MongoHost"]);
      var mongoServer = mongoClient.GetServer();
      var mongoDb = mongoServer.GetDatabase("versace");
      Database db = new Database(mongoDb);
      var protoRun = db.Get<ProtoRun>(req.ProtoRunId);

      var dataSets = DataPreprocessing.MakeTrainingAndValidationSets(req.Symbol, req.StartDate, req.EndDate, req.ValidationPct, GetSignalFunc(req.SignalType));

      var run = Functions.Evolve(protoRun, new DistrubutedTrainer(), dataSets.Item1, dataSets.Item2, completedGeneration => {
        Rabbit.SendMasterUpdate(new MasterUpdate(completedGeneration.Id, completedGeneration.Evaluated.Fitness));
        onGenerationComplete();
      });

      Rabbit.SendMasterResult(new MasterResult(run.Id));
    }

    public static Func<DataSeries<Bar>, double> GetSignalFunc(SignalType sigType)
    {
      if (sigType == SignalType.NextClose)
        return Signals.NextClose;
      throw new NotImplementedException("Unexpected signal type: " + sigType);
    }
  }
}