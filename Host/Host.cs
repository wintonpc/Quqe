using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using HostLib;

namespace Host
{
  class Host
  {
    const string StartEvolutionCommand = "StartEvolution";
    const string StopEvolutionCommand = "StopEvolution";
    const string ShutdownCommand = "Shutdown";

    static void Main(string[] cmdLine)
    {
      var cmd = cmdLine[0];
      if (cmd == "start")
        SendBroadcast(StartEvolutionCommand);
      else if (cmd == "stop")
        SendBroadcast(StopEvolutionCommand);
      else if (cmd == "shutdown")
        SendBroadcast(ShutdownCommand);
      else if (cmd == "controller")
        RunController();
      else
        Console.WriteLine("Unknown command: " + cmd);
    }

    static void SendBroadcast(string msg)
    {
      using (var rabbit = new Rabbit())
        rabbit.SendBroadcast(msg);
    }

    static void RunController()
    {
      using (var rabbit = new Rabbit())
      {
        bool shouldContinue = true;
        while (shouldContinue)
        {
          try
          {
            Console.WriteLine("Waiting for evolution to start");
            WaitForEvolutionToStart(rabbit);
            shouldContinue = AppDomainIsolator.Run(rabbit, rab => {
              var nodeName = Guid.NewGuid().ToString();
              rab.SendBroadcast("NodeUp " + nodeName);
              Console.WriteLine("NodeUp " + nodeName);
              try
              {
                using (new Supervisor(Environment.ProcessorCount))
                  WaitForEvolutionToStop(rab);
              }
              finally
              {
                rab.SendBroadcast("NodeDown " + nodeName);
                Console.WriteLine("NodeDown " + nodeName);
              }
              return true;
            });
          }
          catch (ShutdownException)
          {
            Console.WriteLine("Shutting down");
          }
        }
      }
    }

    static void WaitForEvolutionToStart(Rabbit rabbit)
    {
      while (true)
      {
        var msg = rabbit.GetBroadcast();
        if (msg == StartEvolutionCommand)
          return;
        if (msg == ShutdownCommand)
          throw new ShutdownException();
      }
    }

    static void WaitForEvolutionToStop(Rabbit rabbit)
    {
      while (true)
      {
        var msg = rabbit.GetBroadcast();
        if (msg == StopEvolutionCommand)
          return;
        if (msg == ShutdownCommand)
          throw new ShutdownException();
      }
    }
  }

  [Serializable]
  class ShutdownException : Exception
  {
    public ShutdownException() { }
    ShutdownException(SerializationInfo info, StreamingContext context) : base(info, context) { }
  }
}