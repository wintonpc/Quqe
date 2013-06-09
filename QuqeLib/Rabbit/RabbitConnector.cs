using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  public abstract class RabbitConnector : IDisposable
  {
    protected readonly string Host;
    protected IConnection Connection;
    protected IModel Model;

    protected RabbitConnector(string host)
    {
      Host = host;
      Connection = new ConnectionFactory { HostName = host }.CreateConnection();
      Model = Connection.CreateModel();
    }

    protected bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      BeforeDispose();
      Disposal.DisposeSafely(ref Model);
      Disposal.DisposeSafely(ref Connection);
    }

    protected virtual void BeforeDispose()
    {
    }
  }
}
