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
  public class WorkQueueConsumer : RabbitConnector
  {
    readonly QueueConsumer Consumer;

    public WorkQueueConsumer(WorkQueueInfo wq)
      : base(wq.Host)
    {
      WorkQueueHelpers.DeclareQueue(wq, Model);
      Consumer = new QueueConsumer(wq.Host, wq.Name, true, 2);
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

    /// <summary>Can be called by any thread</summary>
    public void Cancel()
    {
      Consumer.Cancel();
    }

    protected override void BeforeDispose()
    {
      Consumer.Dispose();
      base.BeforeDispose();
    }
  }
}