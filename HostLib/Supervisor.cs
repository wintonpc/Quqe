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
    readonly List<Task> SlaveTasks = new List<Task>();
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
      Func<Task> startNewSlaveTask = () => Task.Factory.StartNew(() => new Slave());

      MasterTask = Task.Factory.StartNew(() => new Master(() => Cancellation.Token.ThrowIfCancellationRequested()));
      for (int i = 0; i < SlaveCount; i++)
        SlaveTasks.Add(startNewSlaveTask());
      Console.WriteLine("Supervisor started a master and {0} slaves", SlaveCount);

      while (true)
      {
        var allTasks = new[] { MasterTask }.Concat(SlaveTasks).ToArray();
        var taskIdx = Task.WaitAny(allTasks);
        if (_isDisposed)
          break;
        if (taskIdx != 0)
        {
          SlaveTasks.RemoveAt(taskIdx - 1);
          SlaveTasks.Add(startNewSlaveTask());
        }
      }

      Task.WaitAll(SlaveTasks.ToArray());
    }

    void StopWorkers()
    {
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