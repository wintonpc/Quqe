using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe.Rabbit
{
  public class SyncConsumer : IDisposable
  {
    readonly AsyncConsumer Consumer;
    Queue<RabbitMessage> Messages = new Queue<RabbitMessage>();
    bool IsCancelled = false;

    public SyncConsumer(ConsumerInfo ci)
    {
      Consumer = new AsyncConsumer(ci, msg => Messages.Enqueue(msg));
    }

    public RabbitMessage Receive()
    {
      Waiter.Wait(() => Messages.Any() || IsCancelled);
      if (IsCancelled)
        return new ReceiveWasCancelled();
      else
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
