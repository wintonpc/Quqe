using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using PCW;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;

namespace Quqe.Rabbit
{
  public class WorkQueueProducer : IDisposable
  {
    readonly WorkQueueInfo WorkQueueInfo;
    readonly Queue<RabbitMessage> Outgoing = new Queue<RabbitMessage>();

    IConnection Connection;
    IModel Model;
    IBasicProperties PublishProps;
    object HeartbeatCookie;

    State MyState = State.Connecting;

    public enum State
    {
      Connecting,
      Connected,
      Disposed
    }

    public WorkQueueProducer(WorkQueueInfo wq)
    {
      WorkQueueInfo = wq;

      Heartbeat();
      TryToConnect();
    }

    void TryToConnect()
    {
      if (MyState != State.Connecting)
        return;

      Safely(() => {
        Connection = Helpers.MakeConnection(WorkQueueInfo.Host);
        Model = Connection.CreateModel();
        Helpers.DeclareQueue(Model, WorkQueueInfo.Name, WorkQueueInfo.IsPersistent);
        PublishProps = Model.CreateBasicProperties();
        PublishProps.DeliveryMode = (byte)(WorkQueueInfo.IsPersistent ? 2 : 1);

        MyState = State.Connected;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(true));

        // first, try to send messages that previously failed
        var oldMsgs = new Queue<RabbitMessage>(Outgoing);
        Outgoing.Clear();
        while (oldMsgs.Any() && MyState == State.Connected)
          Send(oldMsgs.Dequeue());
        while (oldMsgs.Any())
          Outgoing.Enqueue(oldMsgs.Dequeue());
      });
    }

    void Heartbeat()
    {
      if (MyState == State.Disposed) return;

      HeartbeatCookie = PumpTimer.DoLater(1000, Heartbeat);
      if (Connection == null || !Connection.IsOpen)
      {
        MyState = State.Connecting;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(false));
        TryToConnect();
      }
    }

    void Safely(Action f)
    {
      Helpers.Safely(f, () => {
        CleanupRabbit();
        MyState = State.Connecting;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(false));
      });
    }

    void CleanupRabbit()
    {
      Disposal.DisposeSafely(ref Connection);
      Disposal.DisposeSafely(ref Model);
    }

    public event Action<bool> IsConnectedChanged;

    public void Send(RabbitMessage msg)
    {
      Safely(() => {
        Outgoing.Enqueue(msg);
        Model.BasicPublish("", WorkQueueInfo.Name, false, PublishProps, Outgoing.Peek().ToUtf8());
        Outgoing.Dequeue();
      });
    }

    public void Dispose()
    {
      if (MyState == State.Disposed) return;
      MyState = State.Disposed;

      PumpTimer.CancelDoLater(HeartbeatCookie);
      CleanupRabbit();
    }
  }
}