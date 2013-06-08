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

namespace QuqeTest
{
  [TestFixture]
  class RabbitTests
  {
    [Test]
    public void RabbitWorkQueue()
    {
      var wq = new WorkQueueInfo("localhost", "testq", false);
      var p = new WorkQueueProducer(wq);
      var q1 = new WorkQueueConsumer(wq);
      var q2 = new WorkQueueConsumer(wq);

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

  class TestMessage : RabbitMessage
  {
    public int A { get; private set; }
    public string B { get; private set; }

    public TestMessage(int a, string b)
    {
      A = a;
      B = b;
    }
  }
}