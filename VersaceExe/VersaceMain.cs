using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using Quqe;
using Quqe.NewVersace;
using Quqe.Rabbit;
using RabbitMQ.Client;
using System.Collections.Generic;
using MongoDB.Driver.Builders;

namespace VersaceExe
{
  static partial class VersaceMain
  {
    static Broadcaster Broadcaster;
    static RabbitHostInfo RabbitHostInfo = RabbitHostInfo.FromAppSettings();
    static string Hostname = Dns.GetHostName();

    static string GetNodeName()
    {
      return Process.GetCurrentProcess().Id + "." + Thread.CurrentThread.ManagedThreadId + "@" + Hostname;
    }

    static void Main(string[] cmdLine)
    {
      using (Broadcaster = MakeBroadcaster())
      {
        var cmd = cmdLine[0];
        if (cmd == "fetch")
          FetchData();
        else if (cmd == "host")
          RunSupervisor();
        else if (cmd == "run")
          DistributedEvolve(cmdLine[1]);
        else if (cmd == "shutdown")
          Shutdown();
        else
          Console.WriteLine("Unknown command: " + cmd);
      }
    }

    static void FetchData()
    {
      var db = Database.GetProductionDatabase(MongoHostInfo.FromAppSettings());
      var coll = db.MongoDatabase.GetCollection("DbBar");
      coll.EnsureIndex(new IndexKeysBuilder().Ascending("Symbol", "Timestamp"));
      var allBars = db.QueryAll<DbBar>(_ => true);
      DateTime firstDate = DateTime.Parse("11/10/2001");
      DateTime lastDate = allBars.Any() ? allBars.OrderByDescending(x => x.Timestamp).First().Timestamp : firstDate;
      var predicted = "DIA";

      var firstFetchDate = lastDate.AddDays(1);
      Console.WriteLine("Fetching from {0:MM/dd/yyyy} to today", firstFetchDate.Date);

      VersaceDataFetching.DownloadData(predicted, firstFetchDate, DateTime.Now.Date);

      Console.Write("Storing in mongo ...");
      var allNewBars = new List<DbBar>();
      foreach (var symbol in DataPreprocessing.GetTickers(predicted))
        foreach (var dbBar in DataImport.LoadVersace(symbol).Select(x => new DbBar(db, symbol, x.Timestamp, x.Open, x.Low, x.High, x.Close, x.Volume)))
          allNewBars.Add(dbBar);
      db.StoreAll(allNewBars);
      Console.WriteLine("done");
    }

    static Broadcaster MakeBroadcaster()
    {
      var b = new Broadcaster(new BroadcastInfo(RabbitHostInfo, "HostMsgs"));
      b.On<HostShutdown>(_ => {
        throw new ShutdownException();
      });
      return b;
    }

    static void Shutdown()
    {
      using (var hostBroadcast = MakeBroadcaster())
        hostBroadcast.Send(new HostShutdown());
    }

    static void RunSupervisor()
    {
      while (true)
      {
        try
        {
          Console.WriteLine("Waiting for evolution to start");
          var hostStartMsg = Broadcaster.WaitFor<HostStartEvolution>();
          AppDomainIsolator.Run(hostStartMsg.MasterRequest.ToUtf8(), masterRequestBytes => {
            RabbitMessageReader.Register(typeof (TrainRequest));
            using (var bcast = MakeBroadcaster())
            {
              var nodeName = GetNodeName();
              Console.WriteLine("HostUp   " + nodeName);
              try
              {
                using (new Supervisor(Environment.ProcessorCount, (MasterRequest)RabbitMessageReader.Read(0, masterRequestBytes)))
                {
                  Console.WriteLine("Waiting for evolution to stop");
                  bcast.WaitFor<HostStopEvolution>();
                  Console.WriteLine();
                  Console.WriteLine("Evolution stopped");
                }
              }
              finally
              {
                Console.WriteLine("HostDown " + nodeName);
              }
            }
          });
        }
        catch (ShutdownException)
        {
          Console.WriteLine("Shutting down");
          return;
        }
      }
    }

    static void DistributedEvolve(string masterRequestPath)
    {
      var masterReq = (MasterRequest)RabbitMessageReader.Read(0, File.ReadAllBytes(masterRequestPath));

      PurgeTrainRequests();

      using (var hostBroadcast = MakeBroadcaster())
      {
        Console.CancelKeyPress += (sender, eventArgs) => {
          if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlC)
            hostBroadcast.Send(new HostStopEvolution());
          if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlBreak)
            hostBroadcast.Send(new HostShutdown());
        };

        hostBroadcast.Send(new HostStartEvolution(masterReq));

        var sw = new Stopwatch();
        sw.Start();

        var db = Database.GetProductionDatabase(MongoHostInfo.FromAppSettings());
        var protoRun = db.QueryOne<ProtoRun>(x => x.Name == masterReq.ProtoRunName);
        var dataSets = DataPreprocessing.LoadTrainingAndValidationSets(db, masterReq.Symbol, masterReq.StartDate, masterReq.EndDate,
                                                                       masterReq.ValidationPct, GetSignalFunc(masterReq.SignalType));

        Console.WriteLine("Data: {0:MM/dd/yyyy} - {1:MM/dd/yyyy}", masterReq.StartDate, masterReq.EndDate);
        Console.WriteLine("Training set: {0} days", dataSets.Item1.Output.Count);
        Console.WriteLine("Validation set: {0} days", dataSets.Item2.Output.Count);

        var run = Functions.Evolve(protoRun, new DistributedTrainer(), dataSets.Item1, dataSets.Item2,
                                   (genNum, completed, total) => Console.WriteLine("Generation {0}: Trained {1} of {2}", genNum, completed, total),
                                   gen => Console.WriteLine("Gen {0} {1}", gen.Order, gen.Evaluated.Fitness));

        hostBroadcast.Send(new HostStopEvolution());

        Console.WriteLine("Finished Run {0} with fitness {1} in {2}", run.Id, run.Generations.Max(x => x.Evaluated.Fitness), sw.Elapsed);
        Console.ReadKey();
      }
    }

    public static void PurgeTrainRequests()
    {
      using (var conn = new ConnectionFactory { HostName = RabbitHostInfo.Hostname, UserName = RabbitHostInfo.Username, Password = RabbitHostInfo.Password }.CreateConnection())
      using (var model = conn.CreateModel())
      {
        try
        {
          model.QueuePurge("TrainRequests");
        }
        catch (Exception)
        {
        }
      }
    }

    internal static Func<DataSeries<Bar>, double> GetSignalFunc(SignalType sigType)
    {
      if (sigType == SignalType.NextClose)
        return Signals.NextClose;
      throw new NotImplementedException("Unexpected signal type: " + sigType);
    }
  }


  [Serializable]
  class ShutdownException : Exception
  {
    public ShutdownException()
    {
    }

    ShutdownException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
  }
}