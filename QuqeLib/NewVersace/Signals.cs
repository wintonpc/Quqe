using System;

namespace Quqe.NewVersace
{
  public static class Signals
  {
    public static double NextClose(DataSeries<Bar> s)
    {
      if (s.Pos == 0) return 0;
      var ideal = Math.Sign(s[0].Close - s[1].Close);
      if (ideal == 0) return 1; // we never predict "no change", so if there actually was no change, consider it a buy
      return ideal;
    }

    public static double Null(DataSeries<Bar> s)
    {
      return 0;
    }
  }
}
