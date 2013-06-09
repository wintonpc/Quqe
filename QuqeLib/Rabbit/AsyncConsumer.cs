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
using RabbitMQ.Util;
using RabbitMQ.Client.Exceptions;
using System.IO;

namespace Quqe.Rabbit
{
  public class AsyncConsumer : IDisposable
  {
    readonly ConsumerInfo ConsumerInfo;
    readonly Action<RabbitMessage> Consume;

    IConnection Connection;
    IModel Model;
    PostingConsumer Consumer;
    object HeartbeatCookie;

    State MyState = State.Connecting;

    public enum State
    {
      Connecting,
      Connected,
      Disposed
    }

    public AsyncConsumer(ConsumerInfo consumerInfo, Action<RabbitMessage> consume)
    {
      ConsumerInfo = consumerInfo;
      Consume = consume;

      Heartbeat();
      TryToConnect();
    }

    void TryToConnect()
    {
      if (MyState != State.Connecting)
        return;

      Safely(() => {
        Connection = new ConnectionFactory {
          HostName = ConsumerInfo.Host,
          RequestedHeartbeat = 1
        }.CreateConnection();
        Model = Connection.CreateModel();
        Helpers.DeclareQueue(Model, ConsumerInfo.QueueName, ConsumerInfo.IsPersistent);
        Model.BasicQos(0, ConsumerInfo.PrefetchCount, false);
        Consumer = new PostingConsumer(Model, Consume);
        Model.BasicConsume(ConsumerInfo.QueueName, !ConsumerInfo.RequireAck, Consumer);

        MyState = State.Connected;
      });
    }

    void Heartbeat()
    {
      if (MyState == State.Disposed) return;

      HeartbeatCookie = PumpTimer.DoLater(1000, Heartbeat);
      if (Connection == null || !Connection.IsOpen)
      {
        MyState = State.Connecting;
        TryToConnect();
      }
    }

    void Safely(Action f)
    {
      Helpers.Safely(f, () =>
      {
        CleanupRabbit();
        MyState = State.Connecting;
      });
    }

    void CleanupRabbit()
    {
      Disposal.DisposeSafely(ref Connection);
      Disposal.DisposeSafely(ref Model);
      Consumer = null;
    }

    public void Ack(RabbitMessage msg)
    {
      if (MyState != State.Connected) return;

      Safely(() => Model.BasicAck(msg.DeliveryTag, false));
    }

    public void Nack(RabbitMessage msg)
    {
      if (MyState != State.Connected) return;

      Safely(() => Model.BasicNack(msg.DeliveryTag, false, true));
    }

    public void Dispose()
    {
      if (MyState == State.Disposed) return;
      MyState = State.Disposed;

      PumpTimer.CancelDoLater(HeartbeatCookie);

      try
      {
        Model.BasicCancel(Consumer.ConsumerTag);
      }
      catch (Exception)
      {
      }
      CleanupRabbit();
    }

    class PostingConsumer : IBasicConsumer
    {
      public string ConsumerTag { get; private set; }
      public IModel Model { get; private set; }
      readonly SyncContext Sync;
      readonly Action<RabbitMessage> Consume;

      public PostingConsumer(IModel model, Action<RabbitMessage> consume)
      {
        Sync = SyncContext.Current;
        Model = model;
        Consume = consume;
      }

      public void HandleBasicConsumeOk(string consumerTag)
      {
        ConsumerTag = consumerTag;
      }

      public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
      {
        Sync.Post(() => Consume(RabbitMessageReader.Read(deliveryTag, body)));
      }

      public void HandleBasicCancelOk(string consumerTag)
      {
      }

      public void HandleBasicCancel(string consumerTag)
      {
      }

      public void HandleModelShutdown(IModel model, ShutdownEventArgs reason)
      {
      }
    }
  }
}