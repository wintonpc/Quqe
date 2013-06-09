using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Quqe.Rabbit
{
  static class Helpers
  {
    public static void DeclareQueue(IModel model, string name, bool isPersistent)
    {
      if (name.StartsWith("amq."))
        return;

      if (isPersistent)
        model.QueueDeclare(name, true, false, false, null);
      else
        model.QueueDeclare(name, false, false, true, null);
    }

    public static void Safely(Action f, Action onFail)
    {
      try
      {
        f();
      }
      catch (BrokerUnreachableException)
      {
        onFail();
      }
      catch (AlreadyClosedException)
      {
        onFail();
      }
      catch (ConnectFailureException)
      {
        onFail();
      }
      catch (OperationInterruptedException)
      {
        onFail();
      }
      catch (IOException)
      {
        onFail();
      }
    }
  }
}