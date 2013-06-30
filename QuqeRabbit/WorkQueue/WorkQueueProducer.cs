using System.Collections.Generic;
using System.Linq;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  public class WorkQueueProducer : RabbitRecoverer
  {
    readonly WorkQueueInfo WorkQueueInfo;
    readonly Queue<RabbitMessage> Outgoing = new Queue<RabbitMessage>();

    IBasicProperties PublishProps;

    public WorkQueueProducer(WorkQueueInfo wq)
    {
      WorkQueueInfo = wq;
      Init();
    }

    protected override void Connect()
    {
      Connection = Helpers.MakeConnection(WorkQueueInfo.Host);
      Model = Connection.CreateModel();
      Helpers.DeclareQueue(Model, WorkQueueInfo.Name, WorkQueueInfo.IsPersistent);
      PublishProps = Model.CreateBasicProperties();
      PublishProps.DeliveryMode = (byte)(WorkQueueInfo.IsPersistent ? 2 : 1);
    }

    protected override void AfterConnect()
    {
      // try to send messages that previously failed
      var oldMsgs = new Queue<RabbitMessage>(Outgoing);
      Outgoing.Clear();
      while (oldMsgs.Any() && MyState == State.Connected)
        Send(oldMsgs.Dequeue());
      while (oldMsgs.Any())
        Outgoing.Enqueue(oldMsgs.Dequeue());
    }

    public void Send(RabbitMessage msg)
    {
      Safely(() => {
        Outgoing.Enqueue(msg);
        if (MyState != State.Connected) return;
        Model.BasicPublish("", WorkQueueInfo.Name, false, PublishProps, Outgoing.Peek().ToUtf8());
        Outgoing.Dequeue();
      });
    }

    protected override void Cleanup()
    {
    }

    protected override void OnDispose()
    {
    }
  }
}