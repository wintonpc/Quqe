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
  public class Broadcaster : RabbitRecoverer
  {
    readonly BroadcastInfo BroadcastInfo;
    readonly List<Hook> Hooks = new List<Hook>();

    IBasicProperties PublishProps;
    AsyncConsumer Consumer;
    string MyQueueName;

    delegate bool Hook(RabbitMessage msg);

    public Broadcaster(BroadcastInfo broadcast)
    {
      BroadcastInfo = broadcast;
      Init();
    }

    protected override void Connect()
    {
      Connection = Helpers.MakeConnection(BroadcastInfo.Host);
      Model = Connection.CreateModel();

      Model.ExchangeDeclare(BroadcastInfo.Channel, ExchangeType.Fanout, false, false, null);
      var q = Model.QueueDeclare("", false, false, true, null);
      Model.QueueBind(q.QueueName, BroadcastInfo.Channel, "");
      MyQueueName = q.QueueName;

      PublishProps = Model.CreateBasicProperties();
      PublishProps.DeliveryMode = 1;

      Consumer = new AsyncConsumer(new ConsumerInfo(BroadcastInfo.Host, MyQueueName, false, false, 4), DispatchMessage);
      Consumer.IsConnectedChanged += isConnected => {
        if (!isConnected)
          ConnectionBroke();
      };

      Waiter.Wait(Consumer.is
    }

    protected override void AfterConnect()
    {
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

    protected override void Cleanup()
    {
      Disposal.Dispose(ref Consumer);
      PublishProps = null;
      MyQueueName = null;
    }

    protected override void OnDispose()
    {
    }
  }
}