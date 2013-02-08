using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe
{
  public static class BarHelpers
  {
    public static bool WaxContains(this Bar b, double v)
    {
      return b.WaxBottom < v && v < b.WaxTop;
    }

    public static double WaxHeight(this Bar b)
    {
      return b.WaxTop - b.WaxBottom;
    }

    public static double WaxMid(this Bar b)
    {
      return (b.WaxTop + b.WaxBottom) / 2;
    }

    public static double UpperWickHeight(this Bar b)
    {
      return b.High - b.WaxTop;
    }

    public static double LowerWickHeight(this Bar b)
    {
      return b.WaxBottom - b.Low;
    }

    public static bool UpperWickOnly(this Bar b)
    {
      return b.Low + b.WaxHeight() * 0.05 > b.WaxBottom && b.High - b.WaxHeight() * 0.1 > b.WaxTop;
    }

    public static bool LowerWickOnly(this Bar b)
    {
      return b.High - b.WaxHeight() * 0.05 < b.WaxTop && b.Low + b.WaxHeight() * 0.1 < b.WaxBottom;
    }
  }
}
