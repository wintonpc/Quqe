using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;
using PCW;

namespace Quqe.Rabbit
{
  public class QueueConsumer : IDisposable
  {
    readonly string Host;
    readonly string QueueName;
    readonly bool RequireAck;
    readonly ushort PrefetchCount;

    IConnection Connection;
    IModel Model;
    QueueingBasicConsumer Consumer;
    CancellationTokenSource Canceller;

    State MyState = State.Connecting;

    public enum State
    {
      Connecting,
      Connected,
      Disposed
    }

    public QueueConsumer(string host, string queueName, bool requireAck, ushort prefetchCount)
    {
      Host = host;
      QueueName = queueName;
      RequireAck = requireAck;
      PrefetchCount = prefetchCount;

      TryToConnect();
    }

    void TryToConnect()
    {
      if (MyState != State.Connecting)
        return;

      CleanupRabbit();

      try
      {
        Connection = new ConnectionFactory { HostName = Host }.CreateConnection();
        Model = Connection.CreateModel();

        Consumer = new QueueingBasicConsumer(Model);
        Model.BasicConsume(QueueName, !RequireAck, Consumer);
        Model.BasicQos(0, PrefetchCount, false);

        MyState = State.Connected;
      }
      catch (ArithmeticException)
      {
        MyState = State.Connecting;
        PumpTimer.DoLater(1000, TryToConnect);
      }
    }

    public RabbitMessage Receive()
    {
      try
      {
        Canceller = new CancellationTokenSource();

        TryReceive:

        if (Canceller.Token.IsCancellationRequested)
          return new ReceiveWasCancelled();

        object obj;
        bool gotOne = false;
        try
        {
          gotOne = Consumer.Queue.Dequeue(1000, out obj);
        }
        catch (ArithmeticException)
        {
          MyState = 
          throw;
        }

        if (!gotOne)
          goto TryReceive;

        if (Canceller.Token.IsCancellationRequested)
          return new ReceiveWasCancelled();

        var delivered = (BasicDeliverEventArgs)obj;
        return RabbitMessageReader.Read(delivered.DeliveryTag, delivered.Body);
      }
      finally
      {
        Canceller = null;
      }
    }

    public void Ack(RabbitMessage msg)
    {
      try
      {
        Model.BasicAck(msg.DeliveryTag, false);
      }
      catch (ArithmeticException)
      {
        MyState = State.Connecting;
        TryToConnect();
      }
    }

    public void Nack(RabbitMessage msg)
    {
      try
      {
        Model.BasicNack(msg.DeliveryTag, false, true);
      }
      catch (ArithmeticException)
      {
        MyState = State.Connecting;
        TryToConnect();
      }
    }

    /// <summary>Can be called by any thread</summary>
    public void Cancel()
    {
      if (Canceller != null)
        Canceller.Cancel();
    }

    void CleanupRabbit()
    {
      Disposal.Dispose(ref Connection);
      Disposal.Dispose(ref Model);
    }

    public void Dispose()
    {
      try
      {
        Model.BasicCancel(Consumer.ConsumerTag);
      }
      catch (Exception)
      {
      }
      CleanupRabbit();
    }
  }
}