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
  public class WorkQueueConsumer : WorkQueueActor
  {
    readonly QueueingBasicConsumer Consumer;
    readonly CancellationTokenSource Canceller;

    public WorkQueueConsumer(WorkQueueInfo wq)
      : base(wq)
    {
      Canceller = new CancellationTokenSource();
      Consumer = new QueueingBasicConsumer(Model);
      Model.BasicConsume(wq.Name, false, Consumer);
      Model.BasicQos(0, 2, false);
    }

    public RabbitMessage Receive()
    {
      TryReceive:

      if (Canceller.Token.IsCancellationRequested)
          return new ReceiveWasCancelled();

      object obj;
      if (!Consumer.Queue.Dequeue(1000, out obj))
        goto TryReceive;

      var delivered = (BasicDeliverEventArgs)obj;
      return RabbitMessageReader.Read(delivered.DeliveryTag, delivered.Body);
    }

    public void Ack(RabbitMessage msg)
    {
      Model.BasicAck(msg.DeliveryTag, false);
    }

    public void Nack(RabbitMessage msg)
    {
      Model.BasicNack(msg.DeliveryTag, false, true);
    }

    /// <summary>Can be called by any thread</summary>
    public void Cancel()
    {
      Canceller.Cancel();
    }

    protected override void BeforeDispose()
    {
      Model.BasicCancel(Consumer.ConsumerTag);
    }
  }
}