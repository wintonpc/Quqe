using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace Host
{
  class Rabbit : IDisposable
  {
    readonly IConnection Connection;
    readonly IModel Model;

    public Rabbit()
    {
      var cf = new ConnectionFactory { HostName = "localhost" };
      Connection = cf.CreateConnection();
      Model = Connection.CreateModel();
      Model.ExchangeDeclare("VersaceBroadcast", ExchangeType.Fanout, false, false, null);
    }

    public void SendBroadcast(string msg)
    {
      var props = Model.CreateBasicProperties();
      Model.BasicPublish("VersaceBroadcast", "", props, Encoding.UTF8.GetBytes(msg));
    }

    public string GetBroadcast()
    {
      var q = Model.QueueDeclare("", false, true, true, null);
      Model.QueueBind(q.QueueName, "VersaceBroadcast", "");
      var consumer = new QueueingBasicConsumer(Model);
      Model.BasicConsume(q.QueueName, true, consumer);
      var result = (BasicDeliverEventArgs)consumer.Queue.Dequeue();
      return Encoding.UTF8.GetString(result.Body);
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
