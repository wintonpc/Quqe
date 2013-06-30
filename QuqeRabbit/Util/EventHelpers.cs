using System;

namespace Quqe
{
  public static class EventHelpers
  {
    public static void Fire(this Action a)
    {
      if (a != null)
        a();
    }

    public static void Fire<T>(this Action<T> a, T arg1)
    {
      if (a != null)
        a(arg1);
    }

    public static void Fire<T1, T2>(this Action<T1, T2> a, T1 arg1, T2 arg2)
    {
      if (a != null)
        a(arg1, arg2);
    }

    public static void Fire<T1, T2, T3>(this Action<T1, T2, T3> a, T1 arg1, T2 arg2, T3 arg3)
    {
      if (a != null)
        a(arg1, arg2, arg3);
    }
  }
}
