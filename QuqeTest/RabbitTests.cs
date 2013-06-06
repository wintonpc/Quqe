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

namespace QuqeTest
{
  [TestFixture]
  class RabbitTests
  {
    [Test]
    public void RabbitWorkQueue()
    {
      var q1 = new RabbitWorkQueue("localhost", "testq", false);
      var q2 = new RabbitWorkQueue("localhost", "testq", false);

      List<RabbitMessage> received = new List<RabbitMessage>();

      var task = Task.Factory.StartNew(() => {
        while (true)
        {
          var msg = q2.Receive();
          received.Add(msg);
          Trace.WriteLine("Got " + msg.ToJson());
          if (msg is ReceiveWasCancelled)
            return;
          q2.Ack(msg);
        }
      });

      q1.Send(new TestMessage(1, "foo"));
      q1.Send(new TestMessage(2, "bar"));
      Thread.Sleep(15000);
      q2.Cancel();
      task.Wait();
      Trace.WriteLine("Task completed");
      ((TestMessage)received[0]).A.ShouldEqual(1);
      ((TestMessage)received[1]).A.ShouldEqual(2);
      received[2].ShouldBeOfType<ReceiveWasCancelled>();
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
