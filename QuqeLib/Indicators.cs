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
          int windowSize = Math.Min(s.Pos, period);
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
        var windowSize = Math.Min(s.Pos, lookback);
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

    public static DataSeries<Value> LinReg(this DataSeries<Value> values, int period, int forecast)
    {
      return values.MapElements<Value>((s, v) => {
        double sumX = (double)period * (period - 1) * 0.5;
        double divisor = sumX * sumX - (double)period * period * (period - 1) * (2 * period - 1) / 6;
        double sumXY = 0;

        for (int count = 0; count < period && s.Pos - count >= 0; count++)
          sumXY += count * s[count];

        double backBarsSum = s.BackBars(period).Sum(x => x.Val);
        double slope = ((double)period * sumXY - sumX * backBarsSum /*SUM(Inputs[0], period)[0]*/) / divisor;
        double intercept = (backBarsSum /*SUM(Inputs[0], period)[0]*/ - slope * sumX) / period;

        return intercept + slope * (period - 1 + forecast);
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

    public static DataSeries<Value> LinRegRel2(this DataSeries<Bar> bars, int atrPeriod, double threshold)
    {
      var volatileCloses = bars.Closes().LinReg(7, 0).Delay(1);
      var volatileOpens = bars.Opens().LinReg(3, 6).From(volatileCloses.First().Timestamp);
      var trendingCloses = bars.Closes().LinReg(11, 0).Delay(1);
      var trendingOpens = bars.Opens().LinReg(5, 6).From(trendingCloses.First().Timestamp);
      var atr = bars.From(trendingCloses.First().Timestamp).ATR(atrPeriod);

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
  }

  public static class Transforms
  {
    public static DataSeries<Value> Closes(this DataSeries<Bar> bars)
    {
      return bars.MapElements<Value>((s, v) => s[0].Close);
    }
    public static DataSeries<Value> Opens(this DataSeries<Bar> bars)
    {
      return bars.MapElements<Value>((s, v) => s[0].Open);
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

    public static DataSeries<Value> ToDataSeries(this IEnumerable<TradeRecord> trades, Func<TradeRecord, double> getValue)
    {
      return new DataSeries<Value>(trades.First().Symbol, trades.Select(t => new Value(t.EntryTime.Date, getValue(t))));
    }
  }



  public enum BarState { Bullish, Bearish, Parabolic, Washout, BullishBounce, BearishBounce }

  public static class States
  {
    enum Trend { Bull, Bear }

    static BarState TrendToState(Trend t) { return t == Trend.Bull ? BarState.Bullish : BarState.Bearish; }

    public static IEnumerable<BarState> AssignStates(DataSeries<Bar> bars)
    {
      BarState s = BarState.Bullish;
      Trend t = Trend.Bull;
      var results = new List<BarState>();
      int lastTrendChangePos = 0;
      DataSeries.Walk(bars, pos => {
        if (pos < 2)
          results.Add(bars[0].IsGreen ? BarState.Bullish : BarState.Bearish);
        else
        {
          var newTrend = bars.BackBars(3).GroupBy(x => x.IsGreen).OrderByDescending(x => x.Count()).First().Key ? Trend.Bull : Trend.Bear;
          if (t != newTrend)
            lastTrendChangePos = pos;
          t = newTrend;

          //Debug.Assert(bars[0].Timestamp != DateTime.Parse("05/28/2010"));
          bool didntDoAnything = false;
          var recentGreenBar = bars.BackBars(3).Skip(1).Where(b => b.IsGreen).FirstOrDefault();

          var variableLookback = Math.Max(2, Math.Min(pos, pos - lastTrendChangePos));
          var backBars = bars.BackBars(variableLookback).Skip(1);
          var normalWaxHeight = backBars.Average(x => x.WaxHeight());
          var recentHigh = backBars.Max(x => x.Max);

          // bullish => parabolic
          if (bars[1].IsGreen && bars[0].IsGreen && bars[0].UpperWickOnly() && bars[0].High > recentHigh
            && bars[0].WaxHeight() > normalWaxHeight
            && bars[1].WaxContains(bars[0].WaxBottom) && bars[0].WaxHeight() > 1.5 * bars[1].WaxHeight())
          {
            results.Add(BarState.Parabolic);
            s = BarState.Parabolic;
          }
          else if (s == BarState.Parabolic)
          {
            // parabolic => parabolic
            if (bars[0].IsGreen && bars[0].UpperWickOnly() && bars[1].WaxContains(bars[0].WaxBottom)
              && bars[1].WaxBottom + bars[1].WaxHeight() * 0.33 < bars[0].WaxBottom && bars[0].WaxHeight() > bars[1].WaxHeight() * 0.85
              && (bars[0].WaxTop - bars[0].Min) / (bars[0].Max - bars[0].Min) < 0.8)
            {
              results.Add(BarState.Parabolic);
              s = BarState.Parabolic;
            }
            // parabolic => bullish
            else
            {
              results.Add(BarState.Bullish);
              s = BarState.Bullish;
            }
          }
          else if (t == Trend.Bull && recentGreenBar != null && bars[0].IsRed && bars[0].LowerWickOnly() && bars[0].WaxHeight() > 1.5 * recentGreenBar.WaxHeight())
          {
            results.Add(BarState.BearishBounce);
            s = BarState.Bearish;
          }
          else
          {
            didntDoAnything = true;
          }

          if (didntDoAnything)
          {
            s = TrendToState(t);
            results.Add(s);
          }
        }
      });

      return results;
    }
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
