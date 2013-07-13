﻿using System;
using System.Configuration;
using System.Threading.Tasks;
using MongoDB.Bson;
using Quqe.NewVersace;
using Quqe.Rabbit;
using VersaceExe;
using System.Threading;
using System.Diagnostics;
using System.Windows.Threading;

namespace Quqe
{
  public class Slave : IDisposable
  {
    public Task Task { get; set; }
    SyncContext TaskSync;
    Dispatcher Dispatcher;
    readonly Database Database;
    readonly DataSet TrainingSet;
    volatile bool Cancelled;
    bool IsStopped;

    public Slave(DataSet trainingSet)
    {
      Database = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
      TrainingSet = trainingSet;
    }

    public void Run()
    {
      TaskSync = SyncContext.Current;
      Dispatcher = Dispatcher.CurrentDispatcher;
      Thread.CurrentThread.Name = "Slave Thread";

      var rabbitHost = ConfigurationManager.AppSettings["RabbitHost"];
      using (var notifications = new Broadcaster(new BroadcastInfo(rabbitHost, "TrainNotifications")))
      using (var requests = new AsyncWorkQueueConsumer(new WorkQueueInfo(rabbitHost, "TrainRequests", false)))
      {
        requests.Received += msg => {
          var req = (TrainRequest)msg;
          Train(req, () => Cancelled);
          requests.Ack(msg);
          Console.Write(".");
          notifications.Send(new TrainNotification(req));
        };

        Waiter.Wait(() => IsStopped);
      }
    }

    void Train(TrainRequest req, Func<bool> cancelled)
    {
      TrainerCommon.Train(Database, new ObjectId(req.MixtureId), TrainingSet, req.Chromosome, cancelled);
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      Cancelled = true;
      TaskSync.Post(() => {
        IsStopped = true;
      });
    }
  }
}