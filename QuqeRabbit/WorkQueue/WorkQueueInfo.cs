using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe.Rabbit
{
  public class WorkQueueInfo
  {
    public readonly string Host;
    public readonly string Name;
    public readonly bool IsPersistent;

    public WorkQueueInfo(string host, string name, bool isPersistent)
    {
      Host = host;
      Name = name;
      IsPersistent = isPersistent;
    }
  }
}
