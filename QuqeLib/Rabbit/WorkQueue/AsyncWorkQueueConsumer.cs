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
  public class AsyncWorkQueueConsumer : RabbitConnector
  {
    readonly AsyncConsumer Consumer;

    public AsyncWorkQueueConsumer(WorkQueueInfo wq)
      : base(wq.Host)
    {
      WorkQueueHelpers.DeclareQueue(wq, Model);
      Consumer = new AsyncConsumer(new ConsumerInfo(wq.Host, wq.Name, true, 2), msg => {
        if (Received != null)
          Received(msg);
      });
    }

    public event Action<RabbitMessage> Received;

    public void Ack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Nack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    protected override void BeforeDispose()
    {
      Consumer.Dispose();
      base.BeforeDispose();
    }
  }
}