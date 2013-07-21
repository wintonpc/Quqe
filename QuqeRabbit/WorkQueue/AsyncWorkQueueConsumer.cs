using System;

namespace Quqe.Rabbit
{
  public class AsyncWorkQueueConsumer : IDisposable
  {
    readonly AsyncConsumer Consumer;

    public AsyncWorkQueueConsumer(WorkQueueInfo wq)
    {
      Consumer = new AsyncConsumer(new ConsumerInfo(wq.Host, wq.Name, true, wq.IsPersistent, 1), msg => Received.Fire(msg));
      Consumer.IsConnectedChanged += x => IsConnectedChanged.Fire(x);
    }

    public event Action<RabbitMessage> Received;

    public event Action<bool> IsConnectedChanged;

    public bool IsConnected { get { return Consumer.IsConnected; } }

    public void Ack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Nack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Dispose()
    {
      Consumer.Dispose();
    }
  }
}