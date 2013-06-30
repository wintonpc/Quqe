using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quqe;
using Quqe.Rabbit;
using VersaceExe;

namespace Quqe
{
  public class Supervisor : IDisposable
  {
    readonly int WorkerCount;
    List<Worker> Workers;
    readonly Thread MyThread;
    SyncContext ThreadSync;
    readonly MasterRequest MasterRequest;

    public Supervisor(int workerCount, MasterRequest masterRequest)
    {
      Console.WriteLine("Supervisor created");
      WorkerCount = workerCount;
      MasterRequest = masterRequest;

      MyThread = new Thread(Supervise) {
        Name = "Supervisor"
      };
      MyThread.Start();
    }

    void Supervise()
    {
      ThreadSync = SyncContext.Current;

      Workers = Lists.Repeat(WorkerCount, _ => {
        Worker worker = new Worker(MasterRequest);
        worker.Task = Task.Factory.StartNew(worker.Run).ContinueWith(t => {
          Workers.RemoveAll(x => x.Task == t);
          if (t.IsFaulted)
            Console.WriteLine("Worker faulted: " + t.Exception);
        });
        return worker;
      });

      Console.WriteLine("Supervisor started {0} workers", WorkerCount);

      Waiter.Wait(() => !Workers.Any());
    }

    void StopWorkers()
    {
      ThreadSync.Post(() => {
        foreach (var s in Workers.ToList())
        {
          s.Dispose();
          Workers.Remove(s);
        }
        Task.WaitAll(Workers.Select(x => x.Task).ToArray());
        Console.WriteLine("done");
      });
      MyThread.Join();
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      Console.Write("Stopping workers...");
      StopWorkers();
    }
  }
}