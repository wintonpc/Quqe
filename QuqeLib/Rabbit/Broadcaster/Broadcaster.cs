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
  public class Broadcaster : RabbitConnector
  {
    BroadcastInfo BroadcastInfo;
    readonly IBasicProperties PublishProps;
    QueueConsumer Consumer;
    string MyQueueName;
    readonly Task ConsumeTask;
    readonly SyncContext Sync;

    delegate bool Hook(RabbitMessage msg);

    List<Hook> Hooks = new List<Hook>();

    public Broadcaster(BroadcastInfo broadcast)
      : base(broadcast.Host)
    {
      Sync = SyncContext.Current;
      BroadcastInfo = broadcast;

      DeclareExchangeAndQueue(broadcast.Channel);

      PublishProps = Model.CreateBasicProperties();
      PublishProps.DeliveryMode = 1;

      ConsumeTask = StartConsuming();
    }

    void DeclareExchangeAndQueue(string exchangeName)
    {
      Model.ExchangeDeclare(exchangeName, ExchangeType.Fanout, false, false, null);
      var q = Model.QueueDeclare("", false, false, true, null);
      Model.QueueBind(q.QueueName, exchangeName, "");
      MyQueueName = q.QueueName;
    }

    Task StartConsuming()
    {
      return Task.Factory.StartNew(() => {
        using (Consumer = new QueueConsumer(Host, MyQueueName, false, 4))
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

    public void Send(RabbitMessage msg)
    {
      Model.BasicPublish(BroadcastInfo.Channel, "", PublishProps, msg.ToUtf8());
    }

    void DispatchMessage(RabbitMessage msg)
    {
      if (!IsDisposed)
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

    protected override void BeforeDispose()
    {
      Consumer.Cancel();
      ConsumeTask.Wait();
      base.BeforeDispose();
    }
  }
}