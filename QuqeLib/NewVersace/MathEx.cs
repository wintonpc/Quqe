using System;

namespace Quqe
{
  public static class MathEx
  {
    public static int Clamp(int min, int max, int value)
    {
      return Math.Min(max, Math.Max(min, value));
    }
  }
}
