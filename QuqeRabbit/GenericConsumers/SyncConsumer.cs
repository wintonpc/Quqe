using System;
using System.Collections.Generic;
using System.Linq;

namespace Quqe.Rabbit
{
  class SyncConsumer : IDisposable
  {
    readonly AsyncConsumer Consumer;
    Queue<RabbitMessage> Messages = new Queue<RabbitMessage>();
    bool IsCancelled = false;

    public SyncConsumer(ConsumerInfo ci)
    {
      Consumer = new AsyncConsumer(ci, msg => Messages.Enqueue(msg));
    }

    public RabbitMessage Receive(int? msTimeout = null)
    {
      bool timedOut = !Waiter.Wait(msTimeout, () => Messages.Any() || IsCancelled);
      if (timedOut)
        return new ReceiveTimedOut();
      if (IsCancelled)
        return new ReceiveWasCancelled();
      return Messages.Dequeue();
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
      IsCancelled = true;
    }

    public void Dispose()
    {
      Consumer.Dispose();
    }
  }
}