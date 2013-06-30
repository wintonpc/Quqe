using System;

namespace Quqe.Rabbit
{
  public class SyncWorkQueueConsumer : IDisposable
  {
    readonly SyncConsumer Consumer;

    public SyncWorkQueueConsumer(WorkQueueInfo wq)
    {
      Consumer = new SyncConsumer(new ConsumerInfo(wq.Host, wq.Name, true, wq.IsPersistent, 2));
    }

    public RabbitMessage Receive(int? msTimeout = null)
    {
      return Consumer.Receive(msTimeout);
    }

    public void Ack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Nack(RabbitMessage msg)
    {
      Consumer.Ack(msg);
    }

    public void Cancel()
    {
      Consumer.Cancel();
    }

    public void Dispose()
    {
      Consumer.Dispose();
    }
  }
}