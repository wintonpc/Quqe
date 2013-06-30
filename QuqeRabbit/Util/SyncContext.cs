using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace Quqe.Rabbit
{
  public class SyncContext
  {
    [ThreadStatic]
    static SyncContext _SyncContext;
    SynchronizationContext _SynchronizationContext;
    public static SyncContext Current
    {
      get
      {
        Ensure();
        return _SyncContext;
      }
    }

    public static void Ensure()
    {
      if (_SyncContext == null)
      {
        if (SynchronizationContext.Current == null)
          SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        _SyncContext = new SyncContext(SynchronizationContext.Current);
      }
    }

    SyncContext(SynchronizationContext sc)
    {
      _SynchronizationContext = sc;
    }

    public void Run()
    {
      Dispatcher.Run();
    }

    public void Stop()
    {
      Dispatcher.ExitAllFrames();
    }

    public void Post(Action f)
    {
      _SynchronizationContext.Post(CallIt, f);
    }

    static void CallIt(object context)
    {
      ((Action)context)();
    }
  }

  static class PumpTimer
  {
    public static object DoLater(int ms, Action f)
    {
      DispatcherTimer t = null;
      t = new DispatcherTimer(TimeSpan.FromMilliseconds(ms), DispatcherPriority.Normal, (s, ea) => {
        t.Stop();
        f();
      }, Dispatcher.CurrentDispatcher);
      t.Start();
      return t;
    }

    public static void CancelDoLater(object cookie)
    {
      ((DispatcherTimer)cookie).Stop();
    }
  }

  public static class Waiter
  {
    public static bool Wait(int? msTimeout, Func<bool> condition)
    {
      return WaitInternal(msTimeout, condition);
    }

    public static void WaitOrDie(int msTimeout, Func<bool> condition)
    {
      if (!WaitInternal(msTimeout, condition))
        throw new Exception("Condition was not met in time");
    }

    public static void Wait(Func<bool> condition)
    {
      WaitInternal(null, condition);
    }

    public static void Wait(int msTimeout)
    {
      WaitInternal(msTimeout, () => false);
    }

    static bool WaitInternal(int? msTimeout, Func<bool> condition)
    {
      bool timedOut = false;
      Debug.Assert(SynchronizationContext.Current is DispatcherSynchronizationContext);
      DispatcherFrame frame = new DispatcherFrame();

      DispatcherHookEventHandler onOperationCompleted = null;
      Action popFrame = () => {
        frame.Continue = false;
        Dispatcher.CurrentDispatcher.Hooks.OperationCompleted -= onOperationCompleted;
      };
      onOperationCompleted = (s, ea) => {
        if (condition())
          popFrame();
      };

      Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => {
        Dispatcher.CurrentDispatcher.Hooks.OperationCompleted += onOperationCompleted;
      }));

      if (msTimeout != null)
        PumpTimer.DoLater(msTimeout.Value, () => {
          timedOut = true;
          popFrame();
        });

      Dispatcher.PushFrame(frame);
      return !timedOut;
    }

    public static bool RunUntil(this SyncContext sync, int msTimeout, Func<bool> condition)
    {
      return WaitInternal(msTimeout, condition);
    }

    public static void RunUntil(this SyncContext sync, Func<bool> condition)
    {
      WaitInternal(null, condition);
    }
  }
}
