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

      Console.WriteLine("Supervisor started {0} slaves", SlaveCount);

      Slaves = Lists.Repeat(SlaveCount, _ => {
        Slave slave = new Slave(MasterRequest);
        slave.Task = Task.Factory.StartNew(slave.Run).ContinueWith(t => {
          Slaves.RemoveAll(x => x.Task == t);
          if (t.IsFaulted)
            Console.WriteLine("Slave faulted: " + t.Exception);
        });
        return slave;
      });

      Waiter.Wait(() => !Slaves.Any());
    }

    void StopSlaves()
    {
      ThreadSync.Post(() => {
        foreach (var s in Slaves.ToList())
        {
          s.Dispose();
          Slaves.Remove(s);
        }
        Task.WaitAll(Slaves.Select(x => x.Task).ToArray());
        Console.WriteLine("done");
      });
      MyThread.Join();
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      Console.Write("Stopping slaves...");
      StopSlaves();
    }
  }
}