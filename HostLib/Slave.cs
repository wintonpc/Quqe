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

    public void Run()
    {
      Requests = new AsyncWorkQueueConsumer(new WorkQueueInfo(ConfigurationManager.AppSettings["RabbitHost"], "TrainRequests", false));

      Requests.Received += msg =>
      {
        Handle((TrainRequest)msg);
        Requests.Ack(msg);
      };

      Waiter.Wait(() => IsDisposed);
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
      Requests.Dispose();
    }
  }
}
