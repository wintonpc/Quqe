using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;

namespace Quqe.Rabbit
{
  public class SyncWorkQueueConsumer : IDisposable
  {
    readonly SyncConsumer Consumer;

    public SyncWorkQueueConsumer(WorkQueueInfo wq)
    {
      Consumer = new SyncConsumer(new ConsumerInfo(wq.Host, wq.Name, true, wq.IsPersistent, 2));
    }

    public RabbitMessage Receive()
    {
      return Consumer.Receive();
    }

    public void Ack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Nack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Cancel()
    {
      Consumer.Cancel();
    }

    public void Dispose()
    {
      Consumer.Dispose();
    }
  }
}