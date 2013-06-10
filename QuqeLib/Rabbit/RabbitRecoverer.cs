using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  public abstract class RabbitRecoverer : IDisposable
  {
    protected IConnection Connection;
    protected IModel Model;

    object HeartbeatCookie;

    protected State MyState { get; private set; }

    protected enum State
    {
      Connecting,
      Connected,
      Disposed
    }

    protected void Init()
    {
      MyState = State.Connecting;
      Heartbeat();
      TryToConnect();
    }

    void TryToConnect()
    {
      if (MyState != State.Connecting)
        return;

      Safely(() => {
        Connect();
        MyState = State.Connected;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(true));
        AfterConnect();
      });
    }

    protected abstract void Connect();

    protected abstract void AfterConnect();

    void Heartbeat()
    {
      if (MyState == State.Disposed) return;

      HeartbeatCookie = PumpTimer.DoLater(1000, Heartbeat);
      if (Connection == null || !Connection.IsOpen)
      {
        MyState = State.Connecting;
        SyncContext.Current.Post(() => IsConnectedChanged.Fire(false));
        TryToConnect();
      }
    }

    protected void Safely(Action f)
    {
      Helpers.Safely(f, ConnectionBroke);
    }

    protected void ConnectionBroke()
    {
      CleanupRabbit();
      MyState = State.Connecting;
      SyncContext.Current.Post(() => IsConnectedChanged.Fire(false));
    }

    void CleanupRabbit()
    {
      Disposal.DisposeSafely(ref Connection);
      Disposal.DisposeSafely(ref Model);
      Cleanup();
    }

    protected abstract void Cleanup();

    public event Action<bool> IsConnectedChanged;

    public void Dispose()
    {
      if (MyState == State.Disposed) return;
      MyState = State.Disposed;

      PumpTimer.CancelDoLater(HeartbeatCookie);

      try
      {
        OnDispose();
      }
      catch (Exception)
      {
      }

      CleanupRabbit();
    }

    protected abstract void OnDispose();
  }
}