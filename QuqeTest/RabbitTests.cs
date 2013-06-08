using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Quqe.Rabbit;
using System.Threading;
using Machine.Specifications;
using List = PCW.List;
using PCW;

namespace QuqeTest
{
  [TestFixture]
  class RabbitTests
  {
    [Test]
    public void RabbitWorkQueue()
    {
      var wq = new WorkQueueInfo("localhost", "testq", false);
      using (var p = new WorkQueueProducer(wq))
      using (var q1 = new WorkQueueConsumer(wq))
      using (var q2 = new WorkQueueConsumer(wq))
      {
        var q1Count = 0;
        var q2Count = 0;

        var task1 = Task.Factory.StartNew(() => {
          while (true)
          {
            var msg = q1.Receive();
            if (msg is ReceiveWasCancelled)
              return;
            q1.Ack(msg);
            q1Count++;
          }
        });

        var task2 = Task.Factory.StartNew(() => {
          while (true)
          {
            var msg = q2.Receive();
            if (msg is ReceiveWasCancelled)
              return;
            q2.Ack(msg);
            q2Count++;
          }
        });

        List.Repeat(1000, _ => p.Enqueue(new TestMessage((int)DateTime.Now.Ticks, "asdf")));

        Thread.Sleep(1000);
        q1.Cancel();
        q2.Cancel();
        Task.WaitAll(new[] { task1, task2 });

        (q1Count + q2Count).ShouldEqual(1000);
        ((double)q1Count / q2Count).ShouldBeCloseTo(1, 0.05);

        Trace.WriteLine("q1 got " + q1Count);
        Trace.WriteLine("q2 got " + q2Count);
      }
    }

    [Test]
    public void IgnoredIfNoHooks()
    {
      WithBroadcaster(b => {
        b.Send(new TestMessage());
        Waiter.Wait(200);
      });
    }

    [Test]
    public void ReceivesBroadcast()
    {
      WithBroadcaster(b => {
        bool gotTest = false;
        bool gotSubtest = false;
        b.On<TestMessage>(x => gotTest = true);
        b.On<SubTestMessage>(x => gotSubtest = true);
        b.Send(new TestMessage());
        Waiter.WaitOrDie(2000, () => gotTest);
        Waiter.Wait(50);
        gotSubtest.ShouldBeFalse();
      });
    }

    [Test]
    public void BroadcastObeysOrder1()
    {
      WithBroadcaster(b =>
      {
        bool gotTest = false;
        bool gotSubtest = false;
        b.On<TestMessage>(x => gotTest = true);
        b.On<SubTestMessage>(x => gotSubtest = true);
        b.Send(new TestMessage());
        Waiter.WaitOrDie(1000, () => gotTest);
        Waiter.Wait(50);
        gotSubtest.ShouldBeFalse();
      });
    }

    [Test]
    public void BroadcastObeysOrder2()
    {
      WithBroadcaster(b =>
      {
        bool gotTest = false;
        bool gotSubtest = false;
        bool gotOther = false;
        b.On<TestMessage>(x => gotTest = true);
        b.On<SubTestMessage>(x => gotSubtest = true);
        b.On<OtherMessage>(x => gotOther = true);
        b.Send(new OtherMessage());
        Waiter.WaitOrDie(1000, () => gotOther);
        Waiter.Wait(50);
        gotTest.ShouldBeFalse();
        gotSubtest.ShouldBeFalse();
      });
    }

    [Test]
    public void BroadcastUnhook()
    {
      WithBroadcaster(b => {
        bool gotTest = false;
        bool gotSubtest = false;
        var testHook = b.On<TestMessage>(x => gotTest = true);
        var subTestHook = b.On<SubTestMessage>(x => gotSubtest = true);

        gotTest = false;
        gotSubtest = false;
        b.Send(new SubTestMessage());
        Waiter.WaitOrDie(1000, () => gotTest);

        b.Unhook(testHook);

        gotTest = false;
        gotSubtest = false;
        b.Send(new SubTestMessage());
        Waiter.WaitOrDie(1000, () => gotSubtest);

        b.Unhook(subTestHook);

        gotTest = false;
        gotSubtest = false;
        b.Send(new SubTestMessage());
        Waiter.Wait(1000, () => gotSubtest).ShouldBeFalse();
      });
    }

    static void WithBroadcaster(Action<Broadcaster> f)
    {
      WithSync(() => {
        var broadcast = new BroadcastInfo("localhost", "BroadcastTestExchange");
        using (var b1 = new Broadcaster(broadcast))
        {
          f(b1);
        }
      });
    }

    static void WithSync(Action f)
    {
      SyncContext.Current.Post(() => {
        f();
        SyncContext.Current.Stop();
      });
      SyncContext.Current.Run();
    }
  }

  class TestMessage : RabbitMessage
  {
    public int A { get; private set; }
    public string B { get; private set; }

    public TestMessage() : this(5, Guid.NewGuid().ToString("N"))
    {
    }

    public TestMessage(int a, string b)
    {
      A = a;
      B = b;
    }
  }

  class SubTestMessage : TestMessage
  {
    public int C { get; private set; }

    public SubTestMessage()
    {
      C = 64;
    }

    public SubTestMessage(int a, string b, int c) : base(a, b)
    {
      C = c;
    }
  }

  class OtherMessage : RabbitMessage
  {
  }
}