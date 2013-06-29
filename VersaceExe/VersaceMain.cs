﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Quqe.NewVersace;
using Workers;
using Quqe;
using Quqe.Rabbit;
using System.IO;
using System.Threading;
using System.Net;

namespace VersaceExe
{
  class HostStartEvolution : RabbitMessage
  {
  }

  class HostStopEvolution : RabbitMessage
  {
  }

  class HostShutdown : RabbitMessage
  {
  }

  static class VersaceMain
  {
    static Broadcaster Broadcaster;
    static string RabbitHost = ConfigurationManager.AppSettings["RabbitHost"];
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
        if (cmd == "host")
          RunSupervisor();
        else if (cmd == "run")
          DistributedEvolve(cmdLine[1]);
        else if (cmd == "shutdown")
          Shutdown();
        else
          Console.WriteLine("Unknown command: " + cmd);
      }
    }

    static Broadcaster MakeBroadcaster()
    {
      var b = new Broadcaster(new BroadcastInfo(RabbitHost, "HostMsgs"));
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
          Broadcaster.WaitFor<HostStartEvolution>();
          AppDomainIsolator.Run(() => {
            using (var bcast = MakeBroadcaster())
            {
              var nodeName = GetNodeName();
              Console.WriteLine("HostUp   " + nodeName);
              try
              {
                using (new Supervisor(Environment.ProcessorCount))
                {
                  Console.WriteLine("Waiting for evolution to stop");
                  bcast.WaitFor<HostStopEvolution>();
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
      var req = (MasterRequest)RabbitMessageReader.Read(0, File.ReadAllBytes(masterRequestPath));

      using (var hostBroadcast = MakeBroadcaster())
      {
        Console.CancelKeyPress += (sender, eventArgs) => {
          if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlC)
            hostBroadcast.Send(new HostStopEvolution());
          if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlBreak)
            hostBroadcast.Send(new HostShutdown());
        };

        hostBroadcast.Send(new HostStartEvolution());

        var sw = new Stopwatch();
        sw.Start();

        var db = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
        var protoRun = db.QueryOne<ProtoRun>(x => x.Name == req.ProtoRunName);

        var dataSets = DataPreprocessing.MakeTrainingAndValidationSets(req.Symbol, req.StartDate, req.EndDate, req.ValidationPct, GetSignalFunc(req.SignalType));

        //var run = Functions.Evolve(protoRun, new DistributedTrainer(), dataSets.Item1, dataSets.Item2, gen => {
        //  Console.WriteLine("Gen {0} {1}", gen.Order, gen.Evaluated.Fitness);
        //});

        Thread.Sleep(-1);

        //Console.WriteLine("Finished Run {0} with fitness {1} in {2}", run.Id, run.Generations.Max(x => x.Evaluated.Fitness), sw.Elapsed);

        hostBroadcast.Send(new HostStopEvolution());
      }
    }

    static Func<DataSeries<Bar>, double> GetSignalFunc(SignalType sigType)
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