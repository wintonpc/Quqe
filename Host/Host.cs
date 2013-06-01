using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HostLib;

namespace Host
{
  class Host
  {
    const string StartEvolutionCommand = "StartEvolution";
    const string StopEvolutionCommand = "StopEvolution";
    const string ReloadCommand = "Reload";
    const string ShutdownCommand = "Shutdown";

    static void Main(string[] cmdLine)
    {
      var cmd = cmdLine[0];
      if (cmd == "start")
        SendBroadcast(StartEvolutionCommand);
      else if (cmd == "stop")
        SendBroadcast(StopEvolutionCommand);
      else if (cmd == "reload")
        SendBroadcast(ReloadCommand);
      else if (cmd == "shutdown")
        SendBroadcast(ShutdownCommand);
      else if (cmd == "controller")
        ReloadRestart(cmdLine, RunController);
      else
        Console.WriteLine("Unknown command: " + cmd);
    }

    static void SendBroadcast(string msg)
    {
      using (var rabbit = new Rabbit())
        rabbit.SendBroadcast(msg);
    }

    static void ReloadRestart(string[] args, Func<string[], bool> f) { while (AppDomainIsolator.Run(args, f)) ; }

    static bool RunController(string[] args)
    {
      var controllerName = Guid.NewGuid().ToString();
      using (var rabbit = new Rabbit())
      {
        try
        {
          rabbit.SendBroadcast("ControllerUp " + controllerName);
          Console.WriteLine("ControllerUp " + controllerName);
          while (true)
          {
            WaitForEvolutionToStart(rabbit);
            using (var supervisor = new Supervisor(Environment.ProcessorCount))
            {
              WaitForEvolutionToStop(rabbit);
              supervisor.Dispose();
            }
          }
        }
        catch (ReloadException)
        {
          Console.WriteLine("Got reload command");
          return true;
        }
        catch (ShutdownException)
        {
          Console.WriteLine("Got shutdown command");
          return false;
        }
        finally
        {
          rabbit.SendBroadcast("ControllerDown " + controllerName);
          Console.WriteLine("ControllerDown " + controllerName);
        }
      }
    }

    static void WaitForEvolutionToStart(Rabbit rabbit) { WaitForBroadcast(rabbit, StartEvolutionCommand); }

    static void WaitForEvolutionToStop(Rabbit rabbit) { WaitForBroadcast(rabbit, StopEvolutionCommand); }

    static void WaitForBroadcast(Rabbit rabbit, string waitMsg)
    {
      while (true)
      {
        var msg = rabbit.GetBroadcast();
        if (msg == waitMsg)
          return;
        if (msg == ReloadCommand)
          throw new ReloadException();
        if (msg == ShutdownCommand)
          throw new ShutdownException();
      }
    }
  }

  class ReloadException : Exception
  {
  }

  class ShutdownException : Exception
  {
  }
}