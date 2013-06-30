using System;
using System.Diagnostics;
using System.Threading;

namespace Quqe.Rabbit
{
  public static class PollWaiter
  {
    public static bool Wait(int timeoutInMs, Func<bool> condition)
    {
      var sw = new Stopwatch();
      sw.Start();
      while (sw.ElapsedMilliseconds < timeoutInMs)
      {
        if (condition())
          return true;
        Thread.Sleep(250);
      }
      return false;
    }
  }
}
