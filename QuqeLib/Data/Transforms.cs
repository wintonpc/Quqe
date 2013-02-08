using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe
{
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

    public static DataSeries<Value> ToDataSeries(this IEnumerable<double> values, DataSeries timeSource)
    {
      return new DataSeries<Value>("", timeSource.Elements.Select(x => x.Timestamp).Zip(values, (t, v) => new Value(t, v)));
    }

    public static DataSeries<Value> NormalizeSma10(this DataSeries<Value> values)
    {
      var sma10 = values.SMA(10);
      return values.ZipElements<Value, Value>(sma10, (v, ma, _) => (v[0] - ma[0]) / ma[0] * 100.0);
    }

    public static DataSeries<Value> NormalizeSma10(this DataSeries<Bar> values, Func<Bar, Value> getValue)
    {
      return values.MapElements<Value>((s, v) => getValue(s[0])).NormalizeSma10();
    }

    public static DataSeries<Value> NormalizeUnit(this DataSeries<Value> values)
    {
      var min = values.Select(x => x.Val).Min();
      var max = values.Select(x => x.Val).Max();
      var range = max - min;
      return values.MapElements<Value>((s, v) => (s[0] - min) / range);
    }

    public static DataSeries<Value> Weighted(this DataSeries<Bar> bars, double wo, double wl, double wh, double wc)
    {
      return bars.MapElements<Value>((s, v) => {
        var b = s[0];
        return (wo * b.Open + wl * b.Low + wh * b.High + wc * b.Close) / (wo + wl + wh + wc);
      });
    }

    public static DataSeries<Value> OpeningWickHeight(this DataSeries<Bar> bars)
    {
      return bars.MapElements<Value>((s, v) => s[0].IsGreen ? (s[0].Open - s[0].Low) : (s[0].High - s[0].Open));
    }

    public static DataSeries<Value> WickStops(this DataSeries<Bar> bars, int period, int cutoff, int smoothing)
    {
      return bars.OpeningWickHeight().MapElements<Value>((s, v) =>
        s.BackBars(period).OrderByDescending(x => x.Val).ElementAt(Math.Min(cutoff, period - 1)).Val).EMA(smoothing + 1);
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
        b[0].IsGreen == (s[0] > 0) ? 1 :
        -1);
    }

    public static double SignalAccuracyPercent(this DataSeries<Bar> bars, DataSeries<Value> signal)
    {
      var accuracy = bars.SignalAccuracy(signal);
      var result = (double)accuracy.Count(x => x > 0) / accuracy.Count(x => x != 0);
      //Trace.WriteLine(string.Format("SignalAccuracyPercent = {0} ({1} of {2} of {3})",
      //  result, accuracy.Count(x => x > 0), accuracy.Count(x => x != 0), accuracy.Count()));
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

    public static DataSeries<Value> ToSimpleSignal(this DataSeries<SignalValue> signal)
    {
      return signal.MapElements<Value>((s, v) =>
        new Value(s[0].Timestamp, s[0].Bias == SignalBias.Buy ? 1 : -1));
    }

    public static DataSeries<SignalValue> ToSignal(this DataSeries<Value> signal)
    {
      return signal.MapElements<SignalValue>((s, v) =>
        new SignalValue(s[0].Timestamp, s[0] >= 0 ? SignalBias.Buy : SignalBias.Sell, SignalTimeOfDay.Open, null, null, null));
    }
  }
}
