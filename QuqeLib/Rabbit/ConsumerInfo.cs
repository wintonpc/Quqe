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
    public readonly ushort PrefetchCount;

    public ConsumerInfo(string host, string queueName, bool requireAck, ushort prefetchCount)
    {
      Host = host;
      QueueName = queueName;
      RequireAck = requireAck;
      PrefetchCount = prefetchCount;
    }
  }
}
