using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace HostLib
{
  public class Rabbit : IDisposable
  {
    readonly IConnection Connection;
    readonly IModel Model;
    readonly string BroadcastQueueName;

    public static readonly string HostBroadcast = "HostBroadcast";
    public static readonly string MasterRequests = "MasterRequests";
    public static readonly string VersaceBroadcast = "VersaceBroadcast";

    public Rabbit(string host)
    {
      var cf = new ConnectionFactory { HostName = host };
      Connection = cf.CreateConnection();
      Model = Connection.CreateModel();
      Model.QueueDeclare(MasterRequests, false, false, true, null);
      Model.ExchangeDeclare(VersaceBroadcast, ExchangeType.Fanout, false, false, null);
      Model.ExchangeDeclare(HostBroadcast, ExchangeType.Fanout, false, false, null);
      var q = Model.QueueDeclare("", false, true, true, null);
      BroadcastQueueName = q.QueueName;
      Model.QueueBind(BroadcastQueueName, VersaceBroadcast, "");
    }

    public MasterRequest TryGetMasterRequest()
    {
      return Receive<MasterRequest>(MasterRequests, false, true);
    }

    public RabbitMessage GetMasterNotification()
    {
      return Receive<RabbitMessage>(BroadcastQueueName, false, false);
    }

    public T Receive<T>(string queueName, bool requiresAck, bool noWait)
      where T : RabbitMessage
    {
      var consumer = new QueueingBasicConsumer(Model);
      Model.BasicConsume(queueName, !requiresAck, Guid.NewGuid().ToString(), consumer);
      try
      {
        BasicDeliverEventArgs msg = null;
        if (noWait)
        {
          object obj;
          if (!consumer.Queue.Dequeue(3000, out obj))
            return null;
          msg = (BasicDeliverEventArgs)obj;
        }
        else
          msg = (BasicDeliverEventArgs)consumer.Queue.Dequeue();
        return (T)RabbitMessageReader.Read(msg.DeliveryTag, msg.Body);
      }
      finally
      {
        Model.BasicCancel(consumer.ConsumerTag);
      }
    }

    public void SendToQueue(string queueName, RabbitMessage msg)
    {
      var props = Model.CreateBasicProperties();
      Model.BasicPublish("", queueName, props, msg.ToUTF8());
    }

    public void SendToExchange(string exchangeName, RabbitMessage msg)
    {
      var props = Model.CreateBasicProperties();
      Model.BasicPublish(exchangeName, "", props, msg.ToUTF8());
    }

    public void SendToExchange(string exchangeName, string msg)
    {
      var props = Model.CreateBasicProperties();
      Model.BasicPublish(exchangeName, "", props, Encoding.UTF8.GetBytes(msg));
    }

    public void SendMasterUpdate(MasterUpdate masterUpdate)
    {
      SendToExchange(VersaceBroadcast, masterUpdate);
    }

    public void SendMasterResult(MasterResult masterResult)
    {
      SendToExchange(VersaceBroadcast, masterResult);
    }

    public void SendMasterRequest(RabbitMessage masterReq)
    {
      SendToQueue(MasterRequests, masterReq);
    }

    public void StartEvolution()
    {
      SendToExchange(HostBroadcast, "StartEvolution");
    }

    bool IsDisposed;

    public void Dispose()
    {
      if (IsDisposed) return;
      IsDisposed = true;
      Connection.Dispose();
      Model.Dispose();
    }
  }
}