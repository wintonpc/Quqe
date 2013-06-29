using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Workers;
using Quqe;
using Quqe.Rabbit;
using System.IO;

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

  class NodeUp : RabbitMessage
  {
    public string NodeName;

    public NodeUp(string nodeName)
    {
      NodeName = nodeName;
    }
  }

  class NodeDown : RabbitMessage
  {
    public string NodeName;

    public NodeDown(string nodeName)
    {
      NodeName = nodeName;
    }
  }

  static class VersaceMain
  {
    static Broadcaster Broadcaster;
    static string RabbitHost = ConfigurationManager.AppSettings["RabbitHost"];

    static void Main(string[] cmdLine)
    {
      using (Broadcaster = MakeBroadcaster())
      {
        var cmd = cmdLine[0];
        if (cmd == "host")
          RunSupervisor();
        else if (cmd == "run")
          RunController(cmdLine[1]);
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
              var nodeName = Guid.NewGuid().ToString();
              bcast.Send(new NodeUp(nodeName));
              Console.WriteLine("NodeUp " + nodeName);
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
                bcast.Send(new NodeDown(nodeName));
                Console.WriteLine("NodeDown " + nodeName);
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

    static void RunController(string masterRequestPath)
    {
      var masterReq = (MasterRequest)RabbitMessageReader.Read(0, File.ReadAllBytes(masterRequestPath));

      using (var hostBroadcast = new Broadcaster(new BroadcastInfo(RabbitHost, "HostMsgs")))
      using (var masterRequests = new WorkQueueProducer(new WorkQueueInfo(RabbitHost, "MasterRequests", false)))
      using (var versaceBroadcast = new Broadcaster(new BroadcastInfo(RabbitHost, "VersaceMsgs")))
      {
        Console.CancelKeyPress += (sender, eventArgs) => {
          if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlC)
            hostBroadcast.Send(new HostStopEvolution());
          if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlBreak)
            hostBroadcast.Send(new HostShutdown());
        };

        masterRequests.Send(masterReq);
        hostBroadcast.Send(new HostStartEvolution());

        var sw = new Stopwatch();
        sw.Start();

        var db = Database.GetProductionDatabase(ConfigurationManager.AppSettings["MongoHost"]);
        while (true)
        {
          RabbitMessage msg = versaceBroadcast.WaitFor<RabbitMessage>();
          if (msg is MasterUpdate)
          {
            var update = (MasterUpdate)msg;
            Console.WriteLine("Gen {0} {1}", update.GenerationNumber, update.Fitness);
          }
          else if (msg is MasterResult)
          {
            var result = (MasterResult)msg;
            var run = db.Get<Run>(result.RunId);
            Console.WriteLine("Finished Run {0} with fitness {1} in {2}", run.Id, run.Generations.Max(x => x.Evaluated.Fitness), sw.Elapsed);
            return;
          }
        }
      }
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