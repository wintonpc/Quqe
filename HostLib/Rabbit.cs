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

    public Rabbit(string host)
    {
      var cf = new ConnectionFactory { HostName = host };
      Connection = cf.CreateConnection();
      Model = Connection.CreateModel();
      Model.QueueDeclare("MasterRequests", false, false, true, null);
      Model.ExchangeDeclare("VersaceBroadcast", ExchangeType.Fanout, false, false, null);
    }

    public MasterRequest TryGetMasterRequest()
    {
      return Receive<MasterRequest>("MasterRequests", false, true);
    }

    public RabbitMessage GetMasterMessage()
    {
      return Receive<RabbitMessage>("MasterRequests", false, false);
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
          msg = (BasicDeliverEventArgs)consumer.Queue.DequeueNoWait(null);
        else
          msg = (BasicDeliverEventArgs)consumer.Queue.Dequeue();
        if (msg == null)
          return null;
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
      SendToExchange("VersaceBroadcast", masterUpdate);
    }

    public void SendMasterResult(MasterResult masterResult)
    {
      SendToExchange("VersaceBroadcast", masterResult);
    }

    public void SendMasterRequest(RabbitMessage masterReq)
    {
      SendToExchange("VersaceBroadcast", masterReq);
    }

    public void StartEvolution()
    {
      SendToExchange("VersaceBroadcast", "StartEvolution");
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