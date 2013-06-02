using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HostLib
{
  class Rabbit : IDisposable
  {
    public void Dispose()
    {
      throw new NotImplementedException();
    }

    public static MasterRequest TryGetMasterRequest()
    {
      throw new NotImplementedException();
    }

    internal static void SendMasterUpdate(MasterUpdate masterUpdate)
    {
      throw new NotImplementedException();
    }

    internal static void SendMasterResult(MasterResult masterResult)
    {
      throw new NotImplementedException();
    }
  }
}
