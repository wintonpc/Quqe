using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe.Rabbit
{
  public class ConsumerInfo
  {
    public readonly string Host;
    public readonly string QueueName;
    public readonly bool RequireAck;
    public readonly bool IsPersistent;
    public readonly ushort PrefetchCount;

    public ConsumerInfo(string host, string queueName, bool requireAck, bool isPersistent, ushort prefetchCount)
    {
      Host = host;
      QueueName = queueName;
      RequireAck = requireAck;
      IsPersistent = isPersistent;
      PrefetchCount = prefetchCount;
    }
  }
}
