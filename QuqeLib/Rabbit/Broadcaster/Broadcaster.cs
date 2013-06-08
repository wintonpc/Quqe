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
    readonly SyncContext Sync;
    readonly BroadcastInfo BroadcastInfo;
    readonly List<Hook> Hooks = new List<Hook>();

    State MyState = State.Connecting;

    IConnection Connection;
    IModel Model;
    IBasicProperties PublishProps;
    QueueConsumer Consumer;
    string MyQueueName;
    Task ConsumeTask;

    delegate bool Hook(RabbitMessage msg);

    public enum State
    {
      Connecting,
      Connected,
      Disposed
    }

    public Broadcaster(BroadcastInfo broadcast)
    {
      Sync = SyncContext.Current;
      BroadcastInfo = broadcast;

      TryToConnect();

      ConsumeTask = Task.Factory.StartNew(() => {
        using (Consumer = new QueueConsumer(BroadcastInfo.Host, MyQueueName, false, 4))
        {
          while (true)
          {
            var msg = Consumer.Receive();
            if (msg is ReceiveWasCancelled)
              return;
            Sync.Post(() => DispatchMessage(msg));
          }
        }
      });
    }

    void TryToConnect()
    {
      if (MyState != State.Connecting)
        return;

      CleanupRabbit();

      try
      {
        Connection = new ConnectionFactory { HostName = BroadcastInfo.Host }.CreateConnection();
        Model = Connection.CreateModel();

        Model.ExchangeDeclare(BroadcastInfo.Channel, ExchangeType.Fanout, false, false, null);
        var q = Model.QueueDeclare("", false, false, true, null);
        Model.QueueBind(q.QueueName, BroadcastInfo.Channel, "");
        MyQueueName = q.QueueName;

        PublishProps = Model.CreateBasicProperties();
        PublishProps.DeliveryMode = 1;

        MyState = State.Connected;
      }
      catch (ArithmeticException)
      {
        MyState = State.Connecting;
        PumpTimer.DoLater(1000, TryToConnect);
      }
    }

    public void Send(RabbitMessage msg)
    {
      if (MyState != State.Connected)
        return;

      try
      {
        Model.BasicPublish(BroadcastInfo.Channel, "", PublishProps, msg.ToUtf8());
      }
      catch (ArithmeticException)
      {
        MyState = State.Connecting;
        TryToConnect();
      }
    }

    void DispatchMessage(RabbitMessage msg)
    {
      if (MyState != State.Disposed)
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
      PublishProps = null;
      MyQueueName = null;
    }

    public void Dispose()
    {
      if (MyState == State.Disposed) return;
      MyState = State.Disposed;
      Consumer.Cancel();
      Disposal.Dispose(ref Consumer);
      ConsumeTask.Wait();
      ConsumeTask = null;
      CleanupRabbit();
    }
  }
}