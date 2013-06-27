using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HostLib
{
  public class Supervisor : IDisposable
  {
    readonly int SlaveCount;
    Task MasterTask;
    readonly List<Slave> Slaves = new List<Slave>();
    readonly Thread MyThread;
    readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

    public Supervisor(int slaveCount)
    {
      Console.WriteLine("Supervisor created");
      SlaveCount = slaveCount;

      MyThread = new Thread(Supervise) {
        Name = "Supervisor"
      };
      MyThread.Start();
    }

    void Supervise()
    {
      Func<Slave> startNewSlave = () => {
        Slave slave = new Slave();
        slave.Task = Task.Factory.StartNew(slave.Run);
        return slave;
      };

      MasterTask = Task.Factory.StartNew(() => Master.Run(() => Cancellation.Token.ThrowIfCancellationRequested()));
      for (int i = 0; i < SlaveCount; i++)
        Slaves.Add(startNewSlave());

      Console.WriteLine("Supervisor started a master and {0} slaves", SlaveCount);

      while (true)
      {
        var slaveTasks = Slaves.Select(x => x.Task);
        var allTasks = (MasterTask == null ? slaveTasks : new[] { MasterTask }.Concat(slaveTasks)).ToArray();
        var taskIdx = Task.WaitAny(allTasks);
        if (_isDisposed)
          break;

        var task = allTasks[taskIdx];
        if (task.IsFaulted)
          Console.WriteLine("Task faulted: " + task.Exception);

        if (task == MasterTask)
        {
          MasterTask = null;
          Console.WriteLine("Master quit.");
        }
        else
        {
          Slaves.RemoveAll(x => x.Task == task);
          Slaves.Add(startNewSlave());
        }
      }

      Console.Write("Waiting for slaves to stop");
      while (Slaves.Any())
      {
        Console.Write("..." + Slaves.Count);
        Console.Out.Flush();
        var idx = Task.WaitAny(Slaves.Select(x => x.Task).ToArray());
        Slaves.RemoveAt(idx);
      }
      Console.WriteLine();

      Console.WriteLine("Waiting for master to stop...");
      if (MasterTask != null)
        Task.WaitAny(new[] { MasterTask });
    }

    void StopWorkers()
    {
      //Console.WriteLine("Supervisor shutting down... ");
      Cancellation.Cancel();
      MyThread.Join();
    }

    bool _isDisposed;

    public void Dispose()
    {
      if (_isDisposed) return;
      _isDisposed = true;
      StopWorkers();
    }
  }
}