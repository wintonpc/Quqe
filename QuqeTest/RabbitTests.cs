using System;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using NUnit.Framework;
using Quqe;
using Quqe.Rabbit;

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
          Lists.Repeat(1000, _ => p.Send(new TestMessage()));

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
                Lists.Repeat(1000, _ => {
                  var msg = c.Receive();
                  c.Ack(msg);
                });
              }
            });
          });

          Lists.Repeat(1000, _ => p.Send(new TestMessage()));

          task.Wait(2000).ShouldBeTrue();
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
          var isConnected = false;
          p.IsConnectedChanged += x => isConnected = x;
          Waiter.Wait(() => isConnected);
          RabbitTestHelper.PurgeQueue("fooQueue");

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
    [Category("requiresAdmin")]
    public void WorkerQueueIsConnectedEvent()
    {
      WithSync(() => {
        var wq = new WorkQueueInfo("localhost", "fooQueue", false);
        using (var c = new AsyncWorkQueueConsumer(wq))
        using (var p = new WorkQueueProducer(wq))
        {
          bool cIsConnected = false;
          bool pIsConnected = false;
          RabbitMessage msg = null;
          c.Received += x => msg = x;
          c.IsConnectedChanged += x => cIsConnected = x;
          p.IsConnectedChanged += x => pIsConnected = x;

          Waiter.Wait(() => cIsConnected && pIsConnected);
          RabbitTestHelper.StopRabbitService();
          Waiter.Wait(() => !cIsConnected && !pIsConnected);

          msg.ShouldBeNull();
          p.Send(new TestMessage());

          RabbitTestHelper.StartRabbitService();
          Waiter.Wait(() => cIsConnected && pIsConnected);

          Waiter.Wait(() => msg != null);
          msg.ShouldBeOfType<TestMessage>();
        }
      });
    }

    [Test]
    [Category("requiresAdmin")]
    public void BroadcasterIsConnectedEvent()
    {
      WithBroadcaster(b => {
        bool bIsConnected = false;
        TestMessage msg = null;
        b.On<TestMessage>(x => msg = x);
        b.IsConnectedChanged += x => bIsConnected = x;

        Waiter.Wait(() => bIsConnected);

        msg.ShouldBeNull();
        b.Send(new TestMessage());
        Waiter.Wait(() => msg != null);
        msg = null;

        RabbitTestHelper.StopRabbitService();
        Waiter.Wait(() => !bIsConnected);
        RabbitTestHelper.StartRabbitService();
        Waiter.Wait(() => bIsConnected);

        msg.ShouldBeNull();
        b.Send(new TestMessage());
        Waiter.Wait(() => msg != null);
      });
    }

    [Test]
    public
    void IgnoredIfNoHooks()
    {
      WithBroadcaster(b => {
        b.Send(new TestMessage());
        Waiter.Wait(200);
      });
    }

    [Test]
    public
    void ReceivesBroadcast()
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

    [Test]
    public void BroadcastWait()
    {
      WithBroadcaster(b => {
        b.Send(new TestMessage());
        b.WaitFor<SubTestMessage>(1000).ShouldBeNull();
        string s = Guid.NewGuid().ToString();
        b.Send(new TestMessage(5, s));
        b.WaitFor<TestMessage>().B.ShouldEqual(s);
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