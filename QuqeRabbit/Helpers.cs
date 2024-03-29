﻿using System;
using System.IO;
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

    public static IConnection MakeConnection(RabbitHostInfo host)
    {
      return new ConnectionFactory { HostName = host.Hostname, RequestedHeartbeat = 5, UserName = host.Username, Password = host.Password }.CreateConnection();
    }
  }
}