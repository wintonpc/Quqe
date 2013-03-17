using System;
using System.Collections.Generic;
using System.Linq;
using PCW;

namespace Quqe
{
  public static class Indicators
  {
    public static DataSeries<Value> SMA(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return s[0];
        else
        {
          int windowSize = Math.Min(s.Pos + 1, period);
          var last = v[1] * windowSize;
          if (s.Pos >= period)
            return (last + s[0] - s[period]) / windowSize;
          else
            return (last + s[0]) / (windowSize + 1);
        }
      });
    }

    public static DataSeries<Value> EMA(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return s[0];
        else
        {
          return s[0] * (2.0 / (1 + period)) + (1 - (2.0 / (1 + period))) * v[1];
        }
      });
    }

    public static DataSeries<Value> ZLEMA(this DataSeries<Bar> bars, int period, Func<Bar, double> barValue)
    {
      return bars.MapElements<Value>((s, v) => barValue(s[0])).ZLEMA(period);
    }

    public static DataSeries<Value> ZLEMA(this DataSeries<Value> barValues, int period)
    {
      var k = 2.0 / (period + 1);
      var oneMinusK = 1 - k;
      int lag = (int)Math.Ceiling((period - 1) / 2.0);

      return barValues.MapElements<Value>((s, v) => {
        if (s.Pos >= lag)
          return k * (2 * s[0] - s[lag]) + (oneMinusK * v[1]);
        else
          return (double)s[0];
      });
    }

    public static DataSeries<Value> Momentum(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        return s[0] - s[Math.Min(s.Pos, period)];
      });
    }

    public static DataSeries<Value> VersaceRSI(this DataSeries<Bar> bars, int period)
    {
      var upAvg = bars.MapElements<Value>((s, v) => s.Pos == 0 ? 0 : Math.Max(s[0].Close - s[1].Close, 0)).EMA(period);
      var downAvg = bars.MapElements<Value>((s, v) => s.Pos == 0 ? 0 : Math.Max(s[1].Close - s[0].Close, 0)).EMA(period);
      return upAvg.ZipElements<Value, Value>(downAvg, (u, d, _) => u.Pos == 0 ? 50 : 100 - (100 / (1 + u[0] / d[0])));
    }

    public static DataSeries<Bar> HeikenAshi(this DataSeries<Bar> bars)
    {
      return bars.MapElements<Bar>((s, ha) => {
        if (s.Pos == 0)
          return new Bar(s[0].Timestamp, s[0].Open, s[0].Low, s[0].High, s[0].Close, s[0].Volume);
        else
        {
          var haOpen = (ha[1].Open + ha[1].Close) / 2.0;
          var haLow = Math.Min(s[0].Low, haOpen);
          var haHigh = Math.Max(s[0].High, haOpen);
          var haClose = (s[0].Open + s[0].Low + s[0].High + s[0].Close) / 4.0;
          return new Bar(s[0].Timestamp, haOpen, haLow, haHigh, haClose, s[0].Volume);
        }
      });
    }

    public static DataSeries<Value> Trend(this DataSeries<Bar> bars, int lookback)
    {
      return bars.MapElements<Value>((s, v) => {
        var windowSize = Math.Min(s.Pos + 1, lookback);
        if (windowSize == 0)
          return s[0].IsGreen ? 1 : -1;
        else
        {
          var bb = s.BackBars(windowSize + 1).Skip(1);
          return Math.Sign(bb.Count(b => b.IsGreen) - bb.Count(b => b.IsRed));
        }
      });
    }

    public static DataSeries<Value> Midpoint(this DataSeries<Bar> bars, Func<Bar, double> getMin, Func<Bar, double> getMax)
    {
      return bars.MapElements<Value>((s, v) => (getMin(s[0]) + getMax(s[0])) / 2.0);
    }

    public static DataSeries<Value> DonchianMin(this DataSeries<Bar> bars, int period, double bloatPct = 0)
    {
      return bars.MapElements<Value>((s, v) => {
        var lookBack = Math.Min(s.Pos + 1, period);
        var m = s.BackBars(lookBack).Min(b => b.Min);
        var avg = (m + s.BackBars(lookBack).Max(b => b.Max)) / 2;
        return avg - (avg - m) * (1 + bloatPct);
      });
    }

    public static DataSeries<Value> DonchianMax(this DataSeries<Bar> bars, int period, double bloatPct = 0)
    {
      return bars.MapElements<Value>((s, v) => {
        var lookBack = Math.Min(s.Pos + 1, period);
        var m = s.BackBars(lookBack).Max(b => b.Max);
        var avg = (m + s.BackBars(lookBack).Min(b => b.Min)) / 2;
        return avg + (m - avg) * (1 + bloatPct);
      });
    }

    public static DataSeries<Value> ATR(this DataSeries<Bar> bars, int period)
    {
      return bars.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return s[0].High - s[0].Low;
        else
        {
          double trueRange = s[0].High - s[0].Low;
          trueRange = Math.Max(Math.Abs(s[0].Low - s[1].Close), Math.Max(trueRange, Math.Abs(s[0].High - s[1].Close)));
          return ((Math.Min(s.Pos + 1, period) - 1) * v[1] + trueRange) / Math.Min(s.Pos + 1, period);
        }
      });
    }

    public static DataSeries<Value> ChaikinVolatility(this DataSeries<Bar> bars, int period)
    {
      return bars
        .MapElements<Value>((s, v) => s[0].High - s[0].Low)
        .EMA(period)
        .MapElements<Value>((s, v) => {
          var lookBack = Math.Min(s.Pos, period);
          return (s[0] - s[lookBack]) / s[lookBack] * 100;
        });
    }

    public static DataSeries<Value> MACD(this DataSeries<Value> values, int fastPeriod, int slowPeriod)
    {
      return values.EMA(fastPeriod).ZipElements<Value, Value>(values.EMA(slowPeriod), (fast, slow, _) => fast[0] - slow[0]);
    }

    public static DataSeries<Value> ReversalVolatility(this DataSeries<Bar> bars, int period)
    {
      return bars.MapElements<Value>((s, v) => {
        var bb = s.BackBars(period).ToList();
        bool lastWasGreen = bb.First().IsGreen;
        double reversalCount = 0;
        for (int i = 1; i < bb.Count; i++)
        {
          if (bb[i].IsGreen != lastWasGreen)
            reversalCount += Math.Pow((double)(period - i) / period, 2);
          lastWasGreen = bb[i].IsGreen;
        }
        return reversalCount;
      });
    }

    public static DataSeries<Value> Reversals2(this DataSeries<Bar> bars, int period, double k)
    {
      return bars.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;

        var totalCount = Math.Min(s.Pos + 1, period);
        double reversal = 0;
        double continuation = 0;
        for (int i = totalCount - 1; i >= 0; i--)
          if (s[i].IsGreen != s[i + 1].IsGreen)
            reversal += (double)(totalCount - i) / totalCount;
          else
            continuation += (double)(totalCount - i) / totalCount;

        return Math.Max(0, reversal - k * continuation);
      });
    }

    public static DataSeries<Value> ReversalIndex(this DataSeries<Bar> bars, int period)
    {
      return bars.MapElements<Value>((s, v) =>
        s.Pos == 0 ? 0 :
        s[0].IsGreen == s[1].IsGreen ? 1 :
        -1).EMA(period);
    }

    public static DataSeries<Value> AntiTrendIndex(this DataSeries<Bar> bars, int slopePeriod, int emaPeriod)
    {
      var lrs = bars.Closes().LinRegSlope(slopePeriod);
      return bars.ZipElements<Value, Value>(lrs, (b, s, v) => (s[0] >= 0) == b[0].IsGreen ? 1 : -1).EMA(emaPeriod);
    }

    public static DataSeries<Value> BarSum1(this DataSeries<Bar> bars, int period)
    {
      double sum = 0;
      return bars.MapElements<Value>((s, v) => {
        if (s.Pos < period)
          sum += s[0].Close - s[0].Open;
        else
          sum = sum - (s[period].Close - s[period].Open) + (s[0].Close - s[0].Open);
        return sum / period;
      });
    }

    public static DataSeries<Value> BarSum2(this DataSeries<Bar> bars, int period)
    {
      return bars.MapElements<Value>((s, v) => {
        int windowSize = Math.Min(s.Pos + 1, period);
        var pBars = s.BackBars(windowSize).ToList();
        var sum = 0.0;
        for (int i = 0; i < windowSize; i++)
          sum += (double)(windowSize - i) / windowSize * (pBars[i].Close - pBars[i].Open);
        return sum / windowSize;
      });
    }

    public static DataSeries<Value> BarSum3(this DataSeries<Bar> bars, int period, int normalizingPeriod, int smoothing)
    {
      var result = bars.MapElements<Value>((s, v) => {
        var normal = s.BackBars(Math.Min(s.Pos + 1, normalizingPeriod)).Max(x => x.WaxHeight());
        int windowSize = Math.Min(s.Pos + 1, period);
        var pBars = s.BackBars(windowSize).ToList();
        var sum = 0.0;
        for (int i = 0; i < windowSize; i++)
          sum += (double)(windowSize - i) / windowSize * (pBars[i].Close - pBars[i].Open) / normal;
        return sum / windowSize * 10;
      });
      return smoothing > 1 ? result.TriangularMA(smoothing) : result;
    }

    public static DataSeries<Value> TriangularMA(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        int windowSize = Math.Min(s.Pos + 1, period);
        var pValues = s.BackBars(windowSize).ToList();
        var sum = 0.0;
        for (int i = 0; i < windowSize; i++)
          sum += (double)(windowSize - i) * pValues[i];
        return sum / windowSize / windowSize;
      });
    }

    public static DataSeries<Value> ParabolicMA(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        int windowSize = Math.Min(s.Pos + 1, period);
        var pValues = s.BackBars(windowSize).ToList();
        var sum = 0.0;
        for (int i = 0; i < windowSize; i++)
          sum += Math.Pow((double)(windowSize - i) / windowSize, 2) * pValues[i];
        return sum / period;
      });
    }

    public static DataSeries<Value> MostCommonBarColor(this DataSeries<Bar> bars, int period)
    {
      return bars.MapElements<Value>((s, v) => {
        var backBars = s.BackBars(Math.Min(s.Pos + 1, period)).ToList();
        var groups = backBars.GroupBy(x => x.IsGreen).ToList();
        var ordered = groups.OrderByDescending(x => x.Count()).ToList();
        var result = ordered.First().Key ? 1 : -1;
        return result;
      });
    }

    public static DataSeries<Value> ConstantLine(this DataSeries<Bar> bars, double value)
    {
      return bars.MapElements<Value>((s, v) => value);
    }

    public static DataSeries<BiValue> Swing(this DataSeries<Bar> bars, int strength, bool revise = true)
    {
      DataSeries<Value> swingHighSwings = bars.MapElements<Value>((s, v) => 0);
      DataSeries<Value> swingLowSwings = bars.MapElements<Value>((s, v) => 0);
      DataSeries<Value> swingHighSeries = bars.MapElements<Value>((s, v) => 0);
      DataSeries<Value> swingLowSeries = bars.MapElements<Value>((s, v) => 0);
      List<double> lastHighCache = new List<double>();
      List<double> lastLowCache = new List<double>();
      double lastSwingHighValue = 0;
      double lastSwingLowValue = 0;
      double currentSwingHigh = 0;
      double currentSwingLow = 0;
      int saveCurrentBar = -1;

      DataSeries<Value> swingHighPlot = bars.MapElements<Value>((s, v) => 0);
      DataSeries<Value> swingLowPlot = bars.MapElements<Value>((s, v) => 0);

      DataSeries.Walk(List.Create<DataSeries>(bars, swingHighSwings, swingLowSwings, swingHighSeries, swingLowSeries, swingHighPlot, swingLowPlot), pos => {
        if (saveCurrentBar != pos)
        {
          swingHighSwings[0] = new Value(bars[0].Timestamp, 0);
          swingLowSwings[0] = new Value(bars[0].Timestamp, 0);

          swingHighSeries[0] = new Value(bars[0].Timestamp, 0);
          swingLowSeries[0] = new Value(bars[0].Timestamp, 0);

          lastHighCache.Add(bars[0].High);
          if (lastHighCache.Count > (2 * strength) + 1)
            lastHighCache.RemoveAt(0);
          lastLowCache.Add(bars[0].Low);
          if (lastLowCache.Count > (2 * strength) + 1)
            lastLowCache.RemoveAt(0);

          if (lastHighCache.Count == (2 * strength) + 1)
          {
            bool isSwingHigh = true;
            double swingHighCandidateValue = (double)lastHighCache[strength];
            for (int i = 0; i < strength; i++)
              if ((double)lastHighCache[i] >= swingHighCandidateValue - double.Epsilon)
                isSwingHigh = false;

            for (int i = strength + 1; i < lastHighCache.Count; i++)
              if ((double)lastHighCache[i] > swingHighCandidateValue - double.Epsilon)
                isSwingHigh = false;

            swingHighSwings[strength] = new Value(bars[strength].Timestamp, isSwingHigh ? swingHighCandidateValue : 0);
            if (isSwingHigh)
              lastSwingHighValue = swingHighCandidateValue;

            if (isSwingHigh)
            {
              currentSwingHigh = swingHighCandidateValue;
              var stop = revise ? strength : 0;
              for (int i = 0; i <= stop; i++)
                swingHighPlot[i] = new Value(bars[i].Timestamp, currentSwingHigh);
            }
            else if (bars[0].High > currentSwingHigh)
            {
              currentSwingHigh = 0.0;
              swingHighPlot[0] = new Value(bars[0].Timestamp, double.NaN);
            }
            else
              swingHighPlot[0] = new Value(bars[0].Timestamp, currentSwingHigh);

            if (isSwingHigh)
            {
              for (int i = 0; i <= strength; i++)
                swingHighSeries[i] = new Value(bars[i].Timestamp, lastSwingHighValue);
            }
            else
            {
              swingHighSeries[0] = new Value(bars[0].Timestamp, lastSwingHighValue);
            }
          }

          if (lastLowCache.Count == (2 * strength) + 1)
          {
            bool isSwingLow = true;
            double swingLowCandidateValue = (double)lastLowCache[strength];
            for (int i = 0; i < strength; i++)
              if ((double)lastLowCache[i] <= swingLowCandidateValue + double.Epsilon)
                isSwingLow = false;

            for (int i = strength + 1; i < lastLowCache.Count; i++)
              if ((double)lastLowCache[i] < swingLowCandidateValue + double.Epsilon)
                isSwingLow = false;

            swingLowSwings[strength] = new Value(bars[strength].Timestamp, isSwingLow ? swingLowCandidateValue : 0);
            if (isSwingLow)
              lastSwingLowValue = swingLowCandidateValue;

            if (isSwingLow)
            {
              currentSwingLow = swingLowCandidateValue;
              var stop = revise ? strength : 0;
              for (int i = 0; i <= stop; i++)
                swingLowPlot[i] = new Value(bars[i].Timestamp, currentSwingLow);
            }
            else if (bars[0].Low < currentSwingLow)
            {
              currentSwingLow = double.MaxValue;
              swingLowPlot[0] = new Value(bars[0].Timestamp, double.NaN);
            }
            else
              swingLowPlot[0] = new Value(bars[0].Timestamp, currentSwingLow);

            if (isSwingLow)
            {
              for (int i = 0; i <= strength; i++)
                swingLowSeries[i] = new Value(bars[i].Timestamp, lastSwingLowValue);
            }
            else
            {
              swingLowSeries[0] = new Value(bars[0].Timestamp, lastSwingLowValue);
            }
          }

          saveCurrentBar = pos;
        }
        else
        {
          if (bars[0].High > bars[strength].High && swingHighSwings[strength] > 0)
          {
            swingHighSwings[strength] = new Value(bars[strength].Timestamp, 0);
            var stop = revise ? strength : 0;
            for (int i = 0; i <= stop; i++)
              swingHighPlot[i] = new Value(bars[i].Timestamp, double.NaN);
            currentSwingHigh = 0.0;
          }
          else if (bars[0].High > bars[strength].High && currentSwingHigh != 0.0)
          {
            swingHighPlot[0] = new Value(bars[0].Timestamp, double.NaN);
            currentSwingHigh = 0.0;
          }
          else if (bars[0].High <= currentSwingHigh)
            swingHighPlot[0] = new Value(bars[0].Timestamp, currentSwingHigh);

          if (bars[0].Low < bars[strength].Low && swingLowSwings[strength] > 0)
          {
            swingLowSwings[strength] = new Value(bars[strength].Timestamp, 0);
            var stop = revise ? strength : 0;
            for (int i = 0; i <= stop; i++)
              swingLowPlot[i] = new Value(bars[i].Timestamp, double.NaN);
            currentSwingLow = double.MaxValue;
          }
          else if (bars[0].Low < bars[strength].Low && currentSwingLow != double.MaxValue)
          {
            swingLowPlot[0] = new Value(bars[0].Timestamp, double.NaN);
            currentSwingLow = double.MaxValue;
          }
          else if (bars[0].Low >= currentSwingLow)
            swingLowPlot[0] = new Value(bars[0].Timestamp, currentSwingLow);
        }
      });

      return swingHighPlot.ZipElements<Value, BiValue>(swingLowPlot, (h, l, v) => new BiValue(l[0], h[0]));
    }

    public static DataSeries<Value> DonchianAvg(this DataSeries<Bar> bars, int period)
    {
      return bars.DonchianMin(period).ZipElements<Value, Value>(bars.DonchianMax(period), (min, max, v) => (min[0] + max[0]) / 2);
    }

    public static DataSeries<Value> DonchianPct(this DataSeries<Bar> bars, int period, Func<Bar, double> getValue)
    {
      var dMin = bars.DonchianMin(period);
      var dMax = bars.DonchianMax(period);
      var newElements = List.Create<Value>();
      DataSeries.Walk(bars, dMin, dMax, pos => {
        newElements.Add(new Value(bars[0].Timestamp, (getValue(bars[0]) - dMin[0]) / (dMax[0] - dMin[0])));
      });
      return new DataSeries<Value>(bars.Symbol, newElements);
    }

    static void LinRegInternal(DataSeries<Value> s, int period, out double slope, out double intercept)
    {
      double sumX = (double)period * (period - 1) * 0.5;
      double divisor = sumX * sumX - (double)period * period * (period - 1) * (2 * period - 1) / 6;
      double sumXY = 0;
      double backBarsSum = 0;

      for (int count = 0; count < period && s.Pos - count >= 0; count++)
      {
        double sc = s[count];
        sumXY += count * sc;
        backBarsSum += sc;
      }

      //double backBarsSum = s.BackBars(period).Sum(x => x.Val);
      slope = ((double)period * sumXY - sumX * backBarsSum /*SUM(Input[0], period)[0]*/) / divisor;
      intercept = (backBarsSum /*SUM(Input[0], period)[0]*/ - slope * sumX) / period;
    }

    public static DataSeries<Value> LinReg(this DataSeries<Value> values, int period, int forecast)
    {
      return values.MapElements<Value>((s, v) => {
        double slope;
        double intercept;
        int trimmedPeriod = Math.Min(period, s.Pos + 1);
        if (trimmedPeriod < 2)
          return s[0];
        LinRegInternal(s, trimmedPeriod, out slope, out intercept);
        return intercept + slope * (trimmedPeriod - 1 + forecast);
      });
    }

    public static DataSeries<Value> LinRegSlope(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        double slope;
        double intercept;
        int trimmedPeriod = Math.Min(period, s.Pos + 1);
        if (trimmedPeriod < 2)
          return s[0];
        if (trimmedPeriod < 2)
          return 0;
        LinRegInternal(s, trimmedPeriod, out slope, out intercept);
        return slope;
      });
    }

    public static DataSeries<Value> RSquared(this DataSeries<Value> values, int period)
    {
      return values.MapElements<Value>((s, v) => {
        double sumX = (double)period * (period - 1) * 0.5;
        double divisor = sumX * sumX - (double)period * period * (period - 1) * (2 * period - 1) / 6;
        double sumXY = 0;
        double sumX2 = 0;
        double sumY2 = 0;

        for (int count = 0; count < period && s.Pos - count >= 0; count++)
        {
          sumXY += count * s[count];
          sumX2 += (count * count);
          sumY2 += (s[count] * s[count]);
        }

        double backBarsSum = s.BackBars(period).Sum(x => x.Val);
        double numerator = (period * sumXY - sumX * backBarsSum);
        double denominator = (period * sumX2 - (sumX * sumX)) * (period * sumY2 - (backBarsSum * backBarsSum));

        if (denominator > 0)
          return Math.Pow((numerator / Math.Sqrt(denominator)), 2);
        else
          return 0;
      });
    }

    public static DataSeries<Value> FOSC(this DataSeries<Value> values, int period, int forecast)
    {
      var tsf = values.LinReg(period, 0);
      return values.ZipElements<Value, Value>(tsf, (s, t, v) => {
        return 100 * ((s[0] - t[0]) / s[0]);
      });
    }

    public static IEnumerable<T> BackBars<T>(this DataSeries<T> bars, int count)
      where T : DataSeriesElement
    {
      for (int i = 0; i < count; i++)
        yield return bars[i];
    }
  }
}
