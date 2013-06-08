using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Quqe.Rabbit
{
  public static class WorkQueueHelpers
  {
    public static void DeclareQueue(WorkQueueInfo wq, IModel model)
    {
      if (wq.IsPersistent)
        model.QueueDeclare(wq.Name, true, false, false, null);
      else
        model.QueueDeclare(wq.Name, false, false, true, null);
    }
  }
}
