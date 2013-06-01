using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Host
{
  static class AppDomainIsolator
  {
    public static void Run(Action f)
    {
      Run(f, a => a());
    }

    public static void Run<T>(T arg1, Action<T> f)
    {
      Run(arg1, a1 => { f(a1); return true; });
    }

    public static R Run<T, R>(T arg1, Func<T, R> f)
    {
      var appDomainSetup = new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase };
      var domain = AppDomain.CreateDomain("AppDomainIsolator" + Guid.NewGuid().ToString("N"), null, appDomainSetup);
      try
      {
        var t = typeof(Runner<T, R>);
        var runner = (Runner<T, R>)domain.CreateInstanceAndUnwrap(t.Assembly.FullName, t.FullName);
        return runner.Run(f, arg1);
      }
      finally
      {
        AppDomain.Unload(domain);
      }
    }

    class Runner<T, R> : MarshalByRefObject
    {
      public R Run(Func<T, R> f, T arg1)
      {
        return f(arg1);
      }
    }
  }
}
