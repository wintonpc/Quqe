using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VersaceExe
{
  static class AppDomainIsolator
  {
    public static void Run(Action f)
    {
      Run(f, a => a());
    }

    public static void Run<T>(T arg1, Action<T> func)
    {
      Run(new Tuple<T, Action<T>>(arg1, func), t => { t.Item2(t.Item1); return true; });
    }

    public static R Run<T, R>(T arg1, Func<T, R> f)
    {
      var appDomainSetup = new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase };
      var domain = AppDomain.CreateDomain("AppDomainIsolator" + Guid.NewGuid().ToString("N"), null, appDomainSetup);
      Console.WriteLine("Created  " + domain.FriendlyName);
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
        AppDomain.CurrentDomain.DomainUnload += (sender, args) => {
          Console.WriteLine("Unloaded " + AppDomain.CurrentDomain.FriendlyName);
        };
        return f(arg1);
      }
    }
  }
}
