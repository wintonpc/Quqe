using System;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  class AsyncConsumer : RabbitRecoverer
  {
    readonly ConsumerInfo ConsumerInfo;
    readonly Action<RabbitMessage> Consume;

    PostingConsumer Consumer;

    public AsyncConsumer(ConsumerInfo consumerInfo, Action<RabbitMessage> consume)
    {
      ConsumerInfo = consumerInfo;
      Consume = consume;
      Init();
    }

    protected override void Connect()
    {
      Connection = Helpers.MakeConnection(ConsumerInfo.Host);
      Model = Connection.CreateModel();
      Helpers.DeclareQueue(Model, ConsumerInfo.QueueName, ConsumerInfo.IsPersistent);
      Model.BasicQos(0, ConsumerInfo.PrefetchCount, false);
      Consumer = new PostingConsumer(Model, Consume);
      Model.BasicConsume(ConsumerInfo.QueueName, !ConsumerInfo.RequireAck, Consumer);
    }

    protected override void AfterConnect()
    {
    }

    protected override void Cleanup()
    {
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

    protected override void OnDispose()
    {
      Model.BasicCancel(Consumer.ConsumerTag);
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