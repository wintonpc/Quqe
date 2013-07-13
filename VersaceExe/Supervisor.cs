using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quqe.Rabbit;
using VersaceExe;
using System.Configuration;
using MathNet.Numerics.LinearAlgebra.Double;

namespace Quqe
{
  public class Supervisor : IDisposable
  {
    readonly int SlaveCount;
    List<Slave> Slaves;
    readonly Thread MyThread;
    SyncContext ThreadSync;
    readonly MasterRequest MasterRequest;

    public Supervisor(int slaveCount, MasterRequest masterRequest)
    {
      Console.WriteLine("Supervisor created");
      SlaveCount = slaveCount;
      MasterRequest = masterRequest;

      MyThread = new Thread(Supervise) {
        Name = "Supervisor"
      };
      MyThread.Start();
    }

    void Supervise()
    {
      ThreadSync = SyncContext.Current;

      Console.Write("Supervisor is loading dataset...");
      var db = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
      var trainingSet = DataPreprocessing.LoadTrainingAndValidationSets(db, MasterRequest.Symbol, MasterRequest.StartDate,
                                                                     MasterRequest.EndDate, MasterRequest.ValidationPct,
                                                                     VersaceMain.GetSignalFunc(MasterRequest.SignalType)).Item1;
      Console.WriteLine("done");

      Console.Write("Supervisor is starting {0} slaves..", SlaveCount);

      Slaves = Lists.Repeat(SlaveCount, _ => {
        Slave slave = new Slave(CloneDataSet(trainingSet));
        slave.Task = Task.Factory.StartNew(slave.Run).ContinueWith(t => {
          Slaves.RemoveAll(x => x.Task == t);
          if (t.IsFaulted)
            Console.WriteLine("Slave faulted: " + t.Exception);
        });
        return slave;
      });

      Console.WriteLine("done");

      Waiter.Wait(() => !Slaves.Any());
    }

    DataSet CloneDataSet(DataSet data)
    {
      return new DataSet(DenseMatrix.OfMatrix(data.Input), DenseVector.OfVector(data.Output), data.DatabaseAInputLength);
    }

    void StopSlaves()
    {
      ThreadSync.Post(() => {
        foreach (var s in Slaves.ToList())
          s.Dispose();
        Task.WaitAll(Slaves.Select(x => x.Task).ToArray());
        Slaves.Clear();
      });
      MyThread.Join();
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      StopSlaves();
    }
  }
}