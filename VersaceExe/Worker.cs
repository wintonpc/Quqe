using System;
using System.Configuration;
using System.Threading.Tasks;
using MongoDB.Bson;
using Quqe.NewVersace;
using Quqe.Rabbit;
using VersaceExe;

namespace Quqe
{
  public class Worker : IDisposable
  {
    public Task Task { get; set; }
    AsyncWorkQueueConsumer Requests;
    SyncContext TaskSync;
    Database Database;
    DataSet TrainingSet;

    public Worker(MasterRequest masterReq)
    {
      Database = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
      var dataSets = DataPreprocessing.MakeTrainingAndValidationSets(masterReq.Symbol, masterReq.StartDate,
        masterReq.EndDate, masterReq.ValidationPct, VersaceMain.GetSignalFunc(masterReq.SignalType));
      TrainingSet = dataSets.Item1;
    }

    public void Run()
    {
      TaskSync = SyncContext.Current;

      Requests = new AsyncWorkQueueConsumer(new WorkQueueInfo(ConfigurationManager.AppSettings["RabbitHost"], "TrainRequests", false));

      Requests.Received += msg =>
      {
        Handle((TrainRequest)msg);
        Requests.Ack(msg);
      };

      Waiter.Wait(() => Requests == null);
    }

    void Handle(TrainRequest req)
    {
      TrainerCommon.Train(Database, new ObjectId(req.MixtureId), TrainingSet, req.Chromosome);
    }

    bool IsDisposed;
    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      TaskSync.Post(() => Disposal.Dispose(ref Requests));
      Task.Wait();
    }
  }
}
