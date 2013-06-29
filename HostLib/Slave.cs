using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quqe.NewVersace;
using Quqe.Rabbit;
using PCW;

namespace Workers
{
  public class Slave : IDisposable
  {
    public Task Task { get; set; }
    AsyncWorkQueueConsumer Requests;
    SyncContext TaskSync;

    public void Run()
    {
      TaskSync = SyncContext.Current;

      Requests = new AsyncWorkQueueConsumer(new WorkQueueInfo(ConfigurationManager.AppSettings["RabbitHost"], "TrainRequests", false));

      Requests.Received += msg =>
      {
        Handle((TrainRequest)msg);
        Requests.Ack(msg);
      };

      Waiter.Wait(() => Requests == null);
    }

    void Handle(TrainRequest req)
    {
      throw new NotImplementedException();
    }

    bool IsDisposed;
    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      TaskSync.Post(() => Disposal.Dispose(ref Requests));
      Task.Wait();
    }
  }
}
