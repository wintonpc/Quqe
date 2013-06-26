using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe
{
  static class Disposal
  {
    public static void Dispose<T>(ref T obj)
      where T : class, IDisposable
    {
      if (obj == null)
        return;
      obj.Dispose();
      obj = null;
    }

    public static void DisposeSafely<T>(ref T obj)
      where T : class, IDisposable
    {
      if (obj == null)
        return;
      try
      {
        obj.Dispose();
      }
      catch (Exception)
      {
      }
      obj = null;
    }

    public static void All(params IDisposable[] objects)
    {
      foreach (var obj in objects.Where(x => x != null))
        obj.Dispose();
    }
  }
}
