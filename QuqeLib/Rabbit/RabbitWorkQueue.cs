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
  public class RabbitWorkQueue : IDisposable
  {
    readonly string QueueName;
    readonly bool IsPersistent;
    readonly IConnection Connection;
    readonly IModel Model;
    readonly QueueingBasicConsumer Consumer;
    readonly IBasicProperties PublishProps;
    string ConsumerTag { get { return Consumer.ConsumerTag; } }
    object Lock = new object();
    bool IsCancelled;

    public RabbitWorkQueue(string host, string queueName, bool persistent)
    {
      QueueName = queueName;
      IsPersistent = persistent;
      Connection = new ConnectionFactory { HostName = host }.CreateConnection();
      Model = Connection.CreateModel();
      if (persistent)
        Model.QueueDeclare(queueName, true, false, false, null);
      else
        Model.QueueDeclare(queueName, false, false, true, null);
      Consumer = new QueueingBasicConsumer(Model);
      Model.BasicConsume(queueName, false, "", Consumer);
      PublishProps = Model.CreateBasicProperties();
      PublishProps.DeliveryMode = (byte)(persistent ? 2 : 1);
      Model.BasicQos(0, 1, false);
    }

    public RabbitMessage Receive()
    {
      TryReceive:

      lock (Lock)
      {
        if (IsCancelled)
        {
          IsCancelled = false;
          return new ReceiveWasCancelled();
        }
      }

      object obj;
      if (!Consumer.Queue.Dequeue(1000, out obj))
        goto TryReceive;

      var delivered = (BasicDeliverEventArgs)obj;
      return RabbitMessageReader.Read(delivered.DeliveryTag, delivered.Body);
    }

    public void Send(RabbitMessage msg)
    {
      Model.BasicPublish("", QueueName, false, PublishProps, msg.ToUTF8());
    }

    public void Ack(RabbitMessage msg)
    {
      Model.BasicAck(msg.DeliveryTag, false);
    }

    public void Nack(RabbitMessage msg)
    {
      Model.BasicNack(msg.DeliveryTag, false, true);
    }

    public void Cancel()
    {
      lock (Lock)
      {
        if (IsCancelled) return;
        IsCancelled = true;
      }
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      Model.Dispose();
      Connection.Dispose();
    }
  }
}