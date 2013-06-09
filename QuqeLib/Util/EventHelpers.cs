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
  }
}
