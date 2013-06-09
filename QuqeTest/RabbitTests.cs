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
using Quqe;

namespace QuqeTest
{
  [TestFixture]
  class RabbitTests
  {
    [Test]
    public void AsyncWorkQueue()
    {
      WithSync(() => {
        var wq = new WorkQueueInfo("localhost", "fooQueue", false);
        using (var c1 = new AsyncWorkQueueConsumer(wq))
        using (var c2 = new AsyncWorkQueueConsumer(wq))
        using (var p = new WorkQueueProducer(wq))
        {
          List.Repeat(1000, _ => p.Send(new TestMessage()));

          var c1Count = 0;
          var c2Count = 0;

          c1.Received += msg => {
            c1Count++;
            c1.Ack(msg);
          };

          c2.Received += msg => {
            c2Count++;
            c2.Ack(msg);
          };

          Waiter.WaitOrDie(1000, () => c1Count + c2Count == 1000);
          ((double)c1Count / c2Count).ShouldBeCloseTo(1.0, 0.05);
        }
      });
    }

    [Test]
    public void SyncWorkQueue()
    {
      WithSync(() => {
        var wq = new WorkQueueInfo("localhost", "fooQueue", false);
        using (var p = new WorkQueueProducer(wq))
        {
          var task = Task.Factory.StartNew(() => {
            WithSync(() => {
              using (var c = new SyncWorkQueueConsumer(wq))
              {
                List.Repeat(1000, _ => {
                  var msg = c.Receive();
                  c.Ack(msg);
                });
              }
            });
          });

          List.Repeat(1000, _ => p.Send(new TestMessage()));

          task.Wait(1000);
        }
      });
    }

    [Test]
    public void SyncWorkQueueCancelling()
    {
      WithSync(() => {
        var wq = new WorkQueueInfo("localhost", "fooQueue", false);
        using (var p = new WorkQueueProducer(wq))
        {
          SyncWorkQueueConsumer c = null;
          var task = Task.Factory.StartNew(() => {
            WithSync(() => {
              using (c = new SyncWorkQueueConsumer(wq))
              {
                var msg = c.Receive();
                msg.ShouldBeOfType<ReceiveWasCancelled>();
                throw new Exception("cancelled");
              }
            });
          });

          Thread.Sleep(500);
          c.Cancel();
          new Action(() => task.Wait()).ShouldThrow<Exception>(x => x.InnerException.Message.ShouldEqual("cancelled"));
        }
      });
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
      WithBroadcaster(b => {
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
      WithBroadcaster(b => {
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

    public TestMessage()
      : this((int)DateTime.Now.Ticks, Guid.NewGuid().ToString("N"))
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