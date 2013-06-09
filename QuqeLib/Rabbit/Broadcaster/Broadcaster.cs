using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  /// <summary>
  /// Sends and received messages broadcast on a channel.
  /// Messages are not durable and do not need acknowledgement.
  /// Messages are dropped when RabbitMQ goes down.
  /// </summary>
  public class Broadcaster : IDisposable
  {
    readonly BroadcastInfo BroadcastInfo;
    readonly List<Hook> Hooks = new List<Hook>();

    State MyState = State.Connecting;

    IConnection Connection;
    IModel Model;
    IBasicProperties PublishProps;
    AsyncConsumer Consumer;
    string MyQueueName;

    delegate bool Hook(RabbitMessage msg);

    public enum State
    {
      Connecting,
      Connected,
      Disposed
    }

    public Broadcaster(BroadcastInfo broadcast)
    {
      BroadcastInfo = broadcast;

      TryToConnect();
    }

    void TryToConnect()
    {
      if (MyState != State.Connecting)
        return;

      CleanupRabbit();

      Safely(() => {
        Connection = Helpers.MakeConnection(BroadcastInfo.Host);
        Model = Connection.CreateModel();

        Model.ExchangeDeclare(BroadcastInfo.Channel, ExchangeType.Fanout, false, false, null);
        var q = Model.QueueDeclare("", false, false, true, null);
        Model.QueueBind(q.QueueName, BroadcastInfo.Channel, "");
        MyQueueName = q.QueueName;

        PublishProps = Model.CreateBasicProperties();
        PublishProps.DeliveryMode = 1;

        Consumer = new AsyncConsumer(new ConsumerInfo(BroadcastInfo.Host, MyQueueName, false, false, 4), DispatchMessage);

        MyState = State.Connected;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(true));
      });
    }

    public void Send(RabbitMessage msg)
    {
      if (MyState != State.Connected) return;

      Safely(() => Model.BasicPublish(BroadcastInfo.Channel, "", PublishProps, msg.ToUtf8()));
    }

    void DispatchMessage(RabbitMessage msg)
    {
      if (MyState == State.Disposed) return;

      foreach (var h in Hooks)
        if (h(msg))
          return;
    }

    public object On<T>(Action<T> handler)
      where T : RabbitMessage
    {
      var hook = MakeHook(handler);
      Hooks.Add(hook);
      return hook;
    }

    public void Unhook(object hook)
    {
      Hooks.Remove((Hook)hook);
    }

    Hook MakeHook<T>(Action<T> typedHook)
      where T : RabbitMessage
    {
      return msg => {
        if (!(msg is T))
          return false;
        typedHook((T)msg);
        return true;
      };
    }

    void CleanupRabbit()
    {
      Disposal.Dispose(ref Connection);
      Disposal.Dispose(ref Model);
      Disposal.Dispose(ref Consumer);
      PublishProps = null;
      MyQueueName = null;
    }

    public event Action<bool> IsConnectedChanged;

    void Safely(Action f)
    {
      try
      {
        f();
      }
      catch (ArithmeticException)
      {
        CleanupRabbit();
        MyState = State.Connecting;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(true));
        PumpTimer.DoLater(1000, TryToConnect);
      }
    }

    public void Dispose()
    {
      if (MyState == State.Disposed) return;
      MyState = State.Disposed;
      CleanupRabbit();
    }
  }
}