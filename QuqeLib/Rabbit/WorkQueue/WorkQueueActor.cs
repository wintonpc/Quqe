using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  public abstract class WorkQueueActor : IDisposable
  {
    protected readonly WorkQueueInfo WorkQueueInfo;
    protected readonly IConnection Connection;
    protected readonly IModel Model;

    protected WorkQueueActor(WorkQueueInfo wq)
    {
      WorkQueueInfo = wq;
      Connection = new ConnectionFactory { HostName = wq.Host }.CreateConnection();
      Model = Connection.CreateModel();
      DeclareQueue();
    }

    protected void DeclareQueue()
    {
      if (WorkQueueInfo.IsPersistent)
        Model.QueueDeclare(WorkQueueInfo.Name, true, false, false, null);
      else
        Model.QueueDeclare(WorkQueueInfo.Name, false, false, true, null);
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      Model.Dispose();
      Connection.Dispose();
    }

    protected virtual void BeforeDispose()
    {
    }
  }
}
