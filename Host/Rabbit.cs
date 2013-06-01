using System.Configuration;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace Host
{
  class Rabbit : MarshalByRefObject, IDisposable
  {
    readonly IConnection Connection;
    readonly IModel Model;

    public Rabbit()
    {
      var hostname = ConfigurationManager.AppSettings["RabbitHost"];
      var cf = new ConnectionFactory { HostName = hostname };
      Console.Write("Connecting to rabbit host '{0}' ...", hostname);
      Connection = cf.CreateConnection();
      Console.WriteLine("Connected.");
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
