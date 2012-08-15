using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.Diagnostics;

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
        return s[0] - s[Math.Min(s.Pos + 1, period)];
      });
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
        var windowSize = Math.Min(s.Pos+1, lookback);
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

        var totalCount = Math.Min(s.Pos+1, period);
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
      slope = ((double)period * sumXY - sumX * backBarsSum /*SUM(Inputs[0], period)[0]*/) / divisor;
      intercept = (backBarsSum /*SUM(Inputs[0], period)[0]*/ - slope * sumX) / period;
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

    public static DataSeries<Value> NeuralNet(this DataSeries<Bar> bars, NeuralNet net,
      IEnumerable<DataSeries<Value>> inputs)
    {
      Value[] result = new Value[bars.Length];
      DataSeries.Walk(inputs.Cast<DataSeries>().Concat(List.Create<DataSeries>(bars)), pos => {
        result[pos] = new Value(bars[0].Timestamp, net.Propagate(inputs.Select(x => x[0].Val))[0]);
      });
      return new DataSeries<Value>(bars.Symbol, result);
    }
  }

  public static class Signals
  {
    public static DataSeries<Value> LinRegRel(this DataSeries<Bar> bars,
      int openPeriod, int openForecast, int closePeriod, int closeForecast)
    {
      var closes = bars.Closes().LinReg(closePeriod, closeForecast).Delay(1);
      var opens = bars.Opens().LinReg(openPeriod, openForecast).From(closes.First().Timestamp);
      return closes.ZipElements<Value, Value>(opens, (c, o, v) => {
        return Math.Sign(o[0] - c[0]);
      });
    }

    public static DataSeries<Value> LinRegRel2(this DataSeries<Bar> bars, int atrPeriod = 10, double threshold = 1.7)
    {
      var volatileCloses = bars.Closes().LinReg(7, 0).Delay(1);
      var volatileOpens = bars.Opens().LinReg(3, 6).From(volatileCloses.First().Timestamp);
      var trendingCloses = bars.Closes().LinReg(11, 0).Delay(1);
      var trendingOpens = bars.Opens().LinReg(5, 6).From(trendingCloses.First().Timestamp);
      var atr = bars.ATR(atrPeriod).Delay(1);

      List<Value> newElements = new List<Value>();
      var bs = bars.From(trendingCloses.First().Timestamp);
      DataSeries.Walk(List.Create<DataSeries>(
        bs, volatileCloses, volatileOpens, trendingCloses, trendingOpens, atr), pos => {
          if (atr[0] > threshold)
            newElements.Add(new Value(bs[0].Timestamp, Math.Sign(volatileOpens[0] - volatileCloses[0])));
          else
            newElements.Add(new Value(bs[0].Timestamp, Math.Sign(trendingOpens[0] - trendingCloses[0])));
        });

      return new DataSeries<Value>(bars.Symbol, newElements);
    }

    public static DataSeries<Value> MomentumMinusFosc(this DataSeries<Bar> bars, int momoPeriod,
      int foscPeriod, int foscForecast, double threshold, double k)
    {
      var momo = bars.Closes().Momentum(momoPeriod).Delay(1);
      var fosc = bars.Opens().FOSC(foscPeriod, foscForecast).From(momo.First().Timestamp);

      List<Value> newElements = new List<Value>();
      var bs = bars.From(momo.First().Timestamp);
      DataSeries.Walk(bs, momo, fosc, pos => {
        var v = k * momo[0] - fosc[0];
        newElements.Add(new Value(bs[0].Timestamp, Math.Abs(v) > threshold ? Math.Sign(v) : 0));
      });

      return new DataSeries<Value>(bars.Symbol, newElements);
    }

    public static DataSeries<Value> ReversalProbability(this DataSeries<Bar> bars, int period,
      double k, double thresh)
    {
      var rev = bars.Reversals2(period, k).Delay(1);
      var bs = bars.From(rev.First().Timestamp);

      return bs.ZipElements<Value, Value>(rev, (b, r, v) => {
        if (b.Pos == 0)
          return 0;
        return r[0] >= thresh ? (b[1].IsGreen ? -1 : 1) : (b[1].IsGreen ? 1 : -1);
      });
    }
  }

  public enum Prediction { Green, Red }

  public static class DtSignals
  {
    public enum Last2BarColor { Green, Red }
    public enum LastBarColor { Green, Red }
    public enum LastBarSize { Small, Medium, Large }
    public enum GapType { NoneLower, NoneUpper, Up, SuperUp, Down, SuperDown }
    public enum EmaSlope { Up, Down }
    public enum Momentum { Positive, Negative }
    public enum Lrr2 { Buy, Sell }
    public enum RSquared { Linear, Nonlinear }
    public enum LinRegSlope { Positive, Negative }

    public static IEnumerable<DtExample> MakeCandleExamples(DataSeries<Bar> carefulBars,
      double smallMax = 0.65, double mediumMax = 1.20,
      double gapPadding = 0, double superGapPadding = 0,
      double smallMaxPct = -0.1, double largeMinPct = 0.1, int sizeAvgPeriod = 10, int enableBarSizeAveraging = 0,
      int emaPeriod = 12, int enableEma = 0,
      int momentumPeriod = 19, int enableMomentum = 0,
      int rSquaredPeriod = 8, double rSquaredThresh = 0.75, int enableRSquared = 0,
      int linRegSlopePeriod = 14, int enableLinRegSlope = 0,
      int enableLrr2 = 0
      )
    {
      List<DtExample> examples = new List<DtExample>();
      var emaSlope = carefulBars.Closes().ZLEMA(emaPeriod).Derivative().Delay(1);
      var momo = carefulBars.Closes().Momentum(momentumPeriod).Delay(1);
      var rsquared = carefulBars.Closes().RSquared(rSquaredPeriod).Delay(1);
      var linRegSlope = carefulBars.Closes().LinRegSlope(linRegSlopePeriod).Delay(1);
      var lrr2 = carefulBars.LinRegRel2(); // already delayed!
      var bs = carefulBars.From(emaSlope.First().Timestamp);
      DataSeries.Walk(
        List.Create<DataSeries>(bs, emaSlope, momo, lrr2), pos => {
          if (pos < 2)
            return;
          var a = new List<object>();
          a.Add(bs[1].IsGreen ? LastBarColor.Green : LastBarColor.Red);
          a.Add(bs[2].IsGreen ? Last2BarColor.Green : Last2BarColor.Red);
          if (enableBarSizeAveraging > 0)
          {
            var avgHeight = bs.BackBars(Math.Min(pos + 1, sizeAvgPeriod + 1)).Skip(1).Average(x => x.WaxHeight());
            var r = (bs[0].WaxHeight() - avgHeight) / avgHeight;
            a.Add(
              r < smallMaxPct ? LastBarSize.Small :
              r > largeMinPct ? LastBarSize.Large :
              LastBarSize.Medium);
          }
          else
          {
            a.Add(
              bs[1].WaxHeight() < smallMax ? LastBarSize.Small :
              bs[1].WaxHeight() < mediumMax ? LastBarSize.Medium :
              LastBarSize.Large);
          }
          a.Add(
            Between(bs[0].Open, bs[1].WaxBottom, bs[1].WaxMid()) ? GapType.NoneLower :
            Between(bs[0].Open, bs[1].WaxMid(), bs[1].WaxTop) ? GapType.NoneUpper :
            Between(bs[0].Open, bs[1].WaxTop + gapPadding, bs[1].High) ? GapType.Up :
            Between(bs[0].Open, bs[1].Low, bs[1].WaxBottom - gapPadding) ? GapType.Down :
            bs[0].Open > bs[1].High + superGapPadding ? GapType.SuperUp :
            bs[0].Open < bs[1].Low - superGapPadding ? GapType.SuperDown :
            GapType.NoneLower);
          if (enableEma > 0)
            a.Add(emaSlope[0] >= 0 ? EmaSlope.Up : EmaSlope.Down);
          if (enableMomentum > 0)
            a.Add(momo[0] >= 0 ? Momentum.Positive : Momentum.Negative);
          if (enableLrr2 > 0)
            a.Add(lrr2[0] >= 0 ? Lrr2.Buy : Lrr2.Sell);
          if (enableLinRegSlope > 0)
            a.Add(linRegSlope[0] >= 0 ? LinRegSlope.Positive : LinRegSlope.Negative);
          if (enableRSquared > 0)
            a.Add(rsquared[0] >= rSquaredThresh ? RSquared.Linear : RSquared.Nonlinear);
          examples.Add(new DtExample(bs[0].Timestamp, bs[0].IsGreen ? Prediction.Green : Prediction.Red, a));
        });
      return examples;
    }

    static bool Between(double v, double low, double high)
    {
      return low <= v && v <= high;
    }

    public static DataSeries<Value> DtCombo(DataSeries<Bar> teachingSet, DataSeries<Bar> validationSet)
    {
      Func<DataSeries<Bar>, IEnumerable<DtExample>> makeExamples = bs => {
        return MakeCandleExamples(bs,
        smallMax: 0.65,
        mediumMax: 1.21,
        gapPadding: 0,
        superGapPadding: 0.4,
        enableEma: 1,
        emaPeriod: 3,
        enableMomentum: 0,
        momentumPeriod: 19,
        enableLrr2: 0,
        enableLinRegSlope: 0,
        linRegSlopePeriod: 14,
        enableRSquared: 0,
        rSquaredPeriod: 8,
        rSquaredThresh: 0.75
        );
      };

      var dt = DecisionTree.Learn(makeExamples(teachingSet), Prediction.Green, 0.50);
      var exs = makeExamples(validationSet);
      var firstDate = exs.First().Timestamp.Value;
      var attribs = exs.Select(x => x.AttributesValues).ToList();
      var bars = validationSet.From(firstDate);
      var lrr2 = validationSet.LinRegRel2(10, 1.7).From(firstDate);
      var newElements = new List<Value>();
      for (int i = 0; i < bars.Length; i++)
      {
        var d = DecisionTree.Decide(attribs[i], dt);
        if (d is string && (string)d == "Unsure")
          newElements.Add(new Value(bars[0].Timestamp, lrr2[i] >= 0 ? 1 : -1));
        else
          newElements.Add(new Value(bars[0].Timestamp, d.Equals(Prediction.Green) ? 1 : -1));
      }

      return new DataSeries<Value>(bars.Symbol, newElements);
    }

    public enum OpenCloseRel { OpenBelowClose, CloseBelowOpen }
    public enum ActualOpenRel { Below, Between, Above }

    public static IEnumerable<DtExample> MakeExamples2(IEnumerable<StrategyParameter> sParams, DataSeries<Bar> carefulBars)
    {
      int toPeriod = sParams.Get<int>("TOPeriod");
      int toForecast = sParams.Get<int>("TOForecast");
      int tcPeriod = sParams.Get<int>("TCPeriod");
      int tcForecast = sParams.Get<int>("TCForecast");
      int voPeriod = sParams.Get<int>("VOPeriod");
      int voForecast = sParams.Get<int>("VOForecast");
      int vcPeriod = sParams.Get<int>("VCPeriod");
      int vcForecast = sParams.Get<int>("VCForecast");
      int atrPeriod = sParams.Get<int>("ATRPeriod");
      double atrThresh = sParams.Get<double>("ATRThresh");

      if (carefulBars.Length < 2)
        return new List<DtExample>();

      List<DtExample> examples = new List<DtExample>();
      var opens = carefulBars.Opens();
      var closes = carefulBars.Closes();
      var tof = opens.LinReg(toPeriod, toForecast);
      var tcf = closes.LinReg(tcPeriod, tcForecast).Delay(1);
      var vof = opens.LinReg(voPeriod, voForecast);
      var vcf = closes.LinReg(vcPeriod, vcForecast).Delay(1);
      var atr = carefulBars.ATR(atrPeriod).Delay(1);

      tof = tof.From(atr.First().Timestamp);
      vof = tof.From(atr.First().Timestamp);

      var bs = carefulBars.From(atr.First().Timestamp);
      DataSeries.Walk(
        List.Create<DataSeries>(bs, tof, tcf, vof, vcf, atr), pos => {
          if (pos < 1)
            return;
          var a = new List<object>();
          DataSeries<Value> open;
          DataSeries<Value> close;
          if (atr[0] > atrThresh)
          {
            open = vof;
            close = vcf;
          }
          else
          {
            open = tof;
            close = tcf;
          }
          a.Add(open[0] < close[0] ? OpenCloseRel.OpenBelowClose : OpenCloseRel.CloseBelowOpen);
          a.Add(
            bs[0].Open < Math.Min(open[0], close[0]) ? ActualOpenRel.Below :
            bs[0].Open > Math.Max(open[0], close[0]) ? ActualOpenRel.Above :
            ActualOpenRel.Between);
          examples.Add(new DtExample(bs[0].Timestamp, bs[0].IsGreen ? Prediction.Green : Prediction.Red, a));
        });
      return examples;
    }
  }

  public static class Transforms
  {
    public static DataSeries<Value> Closes(this DataSeries<Bar> bars)
    {
      return new DataSeries<Value>(bars.Symbol, bars.Elements.Cast<Bar>()
        .Select(x => new Value(x.Timestamp, x.Close)));
    }
    public static DataSeries<Value> Opens(this DataSeries<Bar> bars)
    {
      return new DataSeries<Value>(bars.Symbol, bars.Elements.Cast<Bar>()
        .Select(x => new Value(x.Timestamp, x.Open)));
    }

    public static DataSeries<Value> Derivative(this DataSeries<Value> values)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return s[0] - s[1];
      });
    }

    public static DataSeries<Value> Integral(this DataSeries<Value> values, double c)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return c + s[0];
        else
          return s[0] + v[1];
      });
    }

    public static DataSeries<Value> Delay(this DataSeries<Value> values, int delay)
    {
      DateTime? firstGoodTimestamp = null;
      var delayed = values.MapElements<Value>((s, v) => {
        if (s.Pos == delay)
          firstGoodTimestamp = s[0].Timestamp;
        if (s.Pos < delay)
          return new Value(default(DateTime), 0);
        else
        {
          var b = s[delay];
          return new Value(default(DateTime), b.Val);
        }
      });
      return delayed.From(firstGoodTimestamp.Value);
    }

    public static DataSeries<Bar> Delay(this DataSeries<Bar> bars, int delay)
    {
      DateTime? firstGoodTimestamp = null;
      var delayed = bars.MapElements<Bar>((s, v) => {
        if (s.Pos == delay)
          firstGoodTimestamp = s[0].Timestamp;
        if (s.Pos < delay)
          return new Bar(0, 0, 0, 0, 0);
        else
        {
          var b = s[delay];
          return new Bar(b.Open, b.Low, b.High, b.Close, b.Volume);
        }
      });
      return delayed.From(firstGoodTimestamp.Value);
    }

    public static DataSeries<BiValue> Delay(this DataSeries<BiValue> bars, int delay)
    {
      DateTime? firstGoodTimestamp = null;
      var delayed = bars.MapElements<BiValue>((s, v) => {
        if (s.Pos == delay)
          firstGoodTimestamp = s[0].Timestamp;
        if (s.Pos < delay)
          return new BiValue(0, 0);
        else
        {
          var b = s[delay];
          return new BiValue(b.Low, b.High);
        }
      });
      return delayed.From(firstGoodTimestamp.Value);
    }

    public static DataSeries<Value> SignalAccuracy(this DataSeries<Bar> bars, DataSeries<Value> signal)
    {
      var bs = bars.From(signal.First().Timestamp);
      return bs.ZipElements<Value, Value>(signal, (b, s, v) =>
        s[0] == 0 ? 0 :
        b[0].IsGreen == (s[0] >= 0) ? 1 :
        -1);
    }

    public static double SignalAccuracyPercent(this DataSeries<Bar> bars, DataSeries<Value> signal)
    {
      var accuracy = bars.SignalAccuracy(signal);
      var result = (double)accuracy.Count(x => x > 0) / accuracy.Count(x => x != 0);
      Trace.WriteLine(string.Format("SignalAccuracyPercent = {0} ({1} of {2} of {3})",
        result, accuracy.Count(x => x > 0), accuracy.Count(x => x != 0), accuracy.Count()));
      return result;
    }

    public static DataSeries<Value> Average(this DataSeries<Value> xs, DataSeries<Value> ys)
    {
      return xs.ZipElements<Value, Value>(ys, (x, y, v) => (x[0] + y[0]) / 2.0);
    }

    public static DataSeries<Value> Extrapolate(this DataSeries<Value> orig)
    {
      var slope = orig.Derivative();

      return orig.ZipElements<Value, Value>(slope, (o, s, v) => {
        if (o.Pos == 0)
          return o[0].Val;
        if (o.Pos == 1)
          return o[1].Val;
        return o[1] + s[1];
      });
    }

    public static DataSeries<Value> PercentReturn(this DataSeries<Value> values)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return (s[0] - s[1]) / s[1];
      });
    }

    public static DataSeries<Value> Sign(this DataSeries<Value> values)
    {
      return values.MapElements<Value>((s, v) => Math.Sign(s[0]));
    }

    public static double Variance(this DataSeries<Value> x, DataSeries<Value> y)
    {
      return x.ZipElements<Value, Value>(y, (xs, ys, vs) => Math.Pow(xs[0] - ys[0], 2)).Sum(a => a.Val);
    }

    public static DataSeries<Value> Trim(this DataSeries<Value> values, double value)
    {
      var first = values.First(x => x.Val != value).Timestamp;
      var last = values.Reverse().First(x => x.Val != value).Timestamp;
      return values.From(first).To(last);
    }

    public static DataSeries<Value> ToDataSeries(this IEnumerable<TradeRecord> trades, Func<TradeRecord, double> getValue)
    {
      return new DataSeries<Value>(trades.First().Symbol, trades.Select(t => new Value(t.EntryTime.Date, getValue(t))));
    }

    public static DataSeries<Value> DecisionTreeSignal(object dt, IEnumerable<DtExample> examples)
    {
      return new DataSeries<Value>("DT", examples.Select(x => {
        var d = DecisionTree.Decide(x.AttributesValues, dt);
        double v;
        if (d.Equals("Unsure"))
          v = 0;
        else if (d.Equals(Prediction.Green))
          v = 1;
        else
          v = -1;
        return new Value(x.Timestamp.Value, v);
      }));
    }

    //public static DataSeries<Value> DecisionTreeSignal(this DataSeries<Bar> bars,
    //  IEnumerable<StrategyParameter> sParams, double minMajority, Func<IEnumerable<StrategyParameter>, DataSeries<Bar>, IEnumerable<DtExample>> makeExamples)
    //{
    //  var examples = makeExamples(sParams, bars);
    //  var dt = DecisionTree.Learn(examples, Prediction.Green, minMajority);

    //  return new DataSeries<Value>(bars.Symbol, examples.Select(x => {
    //    var d = DecisionTree.Decide(x.AttributesValues, dt);
    //    double v;
    //    if (d.Equals("Unsure"))
    //      v = 0;
    //    else if (d.Equals(Prediction.Green))
    //      v = 1;
    //    else
    //      v = -1;
    //    return new Value(x.Timestamp.Value, v);
    //  }));
    //}
  }

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
