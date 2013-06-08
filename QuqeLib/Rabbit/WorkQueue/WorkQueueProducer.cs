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
  public class WorkQueueProducer : RabbitConnector, IDisposable
  {
    readonly string QueueName;
    readonly IBasicProperties PublishProps;

    public WorkQueueProducer(WorkQueueInfo wq)
      : base(wq.Host)
    {
      QueueName = wq.Name;
      WorkQueueHelpers.DeclareQueue(wq, Model);
      PublishProps = Model.CreateBasicProperties();
      PublishProps.DeliveryMode = (byte)(wq.IsPersistent? 2 : 1);
    }

    public void Enqueue(RabbitMessage msg)
    {
      Model.BasicPublish("", QueueName, false, PublishProps, msg.ToUtf8());
    }
  }
}