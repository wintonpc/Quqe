﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
          int windowSize = Math.Min(s.Pos, period);
          var last = v[1] * windowSize;
          if (s.Pos >= period)
            return (last + s[0] - s[period]) / windowSize;
          else
            return (last + s[0]) / (windowSize + 1);
        }
      });
    }

    public static DataSeries<Value> ZLEMA(this DataSeries<Bar> bars, int period, Func<Bar, double> barValue)
    {
      var k = 2.0 / (period + 1);
      var oneMinusK = 1 - k;
      int lag = (int)Math.Ceiling((period - 1) / 2.0);

      var barValues = bars.MapElements<Value>((s, v) => barValue(s[0]));

      return barValues.MapElements<Value>((s, v) => {
        if (s.Pos >= lag)
          return k * (2 * s[0] - s[lag]) + (oneMinusK * v[1]);
        else
          return (double)s[0];
      });
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

    public static DataSeries<Value> DonchianAvg(this DataSeries<Bar> bars, int period)
    {
      return bars.DonchianMin(period).ZipElements<Value, Value>(bars.DonchianMax(period), (min, max, v) => (min[0] + max[0]) / 2);
    }

    static IEnumerable<T> BackBars<T>(this DataSeries<T> bars, int count)
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
        result[pos] = net.Propagate(inputs.Select(x => x[0].Val))[0];
      });
      return new DataSeries<Value>(bars.Symbol, result);
    }
  }

  public static class Transforms
  {
    public static DataSeries<Value> Closes(this DataSeries<Bar> bars)
    {
      return bars.MapElements<Value>((s, v) => s[0].Close);
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

    public static DataSeries<Value> PercentReturn(this DataSeries<Value> values)
    {
      return values.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return (s[0] - s[1]) / s[1];
      });
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
}
