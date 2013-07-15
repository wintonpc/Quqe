using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Quqe
{
  public abstract class DataSeriesElement
  {
    public DateTime Timestamp { get; private set; }

    public DataSeriesElement() { }
    public DataSeriesElement(DateTime timestamp)
    {
      Timestamp = timestamp;
    }

    public abstract double Min { get; }
    public abstract double Max { get; }

    public void SetTimestamp(DateTime timestamp)
    {
      if (Timestamp != default(DateTime) && Timestamp != timestamp)
        throw new InvalidOperationException("Timestamp is already set.");
      Timestamp = timestamp;
    }
  }

  public class Bar : DataSeriesElement
  {
    public readonly double Open;
    public readonly double Low;
    public readonly double High;
    public readonly double Close;
    public readonly long Volume;

    public Bar() : base() { throw new NotImplementedException("Don't call this"); }

    public Bar(double open, double low, double high, double close, long volume)
      : base()
    {
      Open = open;
      Low = low;
      High = high;
      Close = close;
      Volume = volume;
    }

    public Bar(DateTime timestamp, double open, double low, double high, double close, long volume)
      : base(timestamp)
    {
      Open = open;
      Low = low;
      High = high;
      Close = close;
      Volume = volume;
    }

    public override double Min { get { return Low; } }
    public override double Max { get { return High; } }
    public double Midpoint { get { return (Open + Close) / 2.0; } }
    public bool IsGreen { get { return Close >= Open; } }
    public bool IsRed { get { return !IsGreen; } }
    public double WaxTop { get { return Math.Max(Open, Close); } }
    public double WaxBottom { get { return Math.Min(Open, Close); } }
  }

  public class BiValue : DataSeriesElement
  {
    public readonly double Low;
    public readonly double High;

    public BiValue() : base() { throw new NotImplementedException("Don't call this"); }

    public BiValue(double low, double high)
      : base()
    {
      Low = low;
      High = high;
    }

    public BiValue(DateTime timestamp, double low, double high)
      : base(timestamp)
    {
      Low = low;
      High = high;
    }

    public override double Min { get { return Low; } }
    public override double Max { get { return High; } }
    public bool HasLow { get { return !double.IsNaN(Low); } }
    public bool HasHigh { get { return !double.IsNaN(High); } }
  }

  public class Value : DataSeriesElement
  {
    public readonly double Val;
    public Value(DateTime timestamp, double value)
      : base(timestamp)
    {
      Val = value;
    }

    public Value() : this(0) { }

    Value(double value)
    {
      Val = value;
    }

    public static implicit operator double(Value x) { return x.Val; }
    public static implicit operator Value(double x) { return new Value(x); }

    public override double Min { get { return Val; } }
    public override double Max { get { return Val; } }
  }

  public enum SignalBias { Buy, Sell, None }
  public enum SignalTimeOfDay { Open, Close }

  public class SignalValue : DataSeriesElement
  {
    public readonly double? Stop;
    public readonly double? Limit;
    public readonly SignalBias Bias;
    public readonly SignalTimeOfDay Time;
    public readonly double SizePct;

    public SignalValue(DateTime timestamp, SignalBias bias, SignalTimeOfDay time, double? sizePct, double? stop, double? limit)
      : base(timestamp)
    {
      Bias = bias;
      Time = time;
      Stop = stop;
      Limit = limit;
      SizePct = sizePct ?? 1.0;
    }

    public SignalValue() { throw new NotImplementedException(); }

    public override double Min { get { return Stop ?? 0; } }
    public override double Max { get { return Stop ?? 0; } }
  }

  public abstract class DataSeries
  {
    public readonly string Symbol;
    public string Tag { get; protected set; }
    public abstract int Length { get; }

    protected int _Pos = 0;
    public int Pos { get { return _Pos; } }
    protected bool IsWalking = false;

    public abstract IEnumerable<DataSeriesElement> Elements { get; }

    readonly Thread OwnerThread;
    public DataSeries(string symbol)
      : this(symbol, Thread.CurrentThread) { }
    protected DataSeries(string symbol, Thread ownerThread)
    {
      Symbol = symbol;
      OwnerThread = ownerThread;
    }

    public abstract DataSeries FromDate(DateTime timestamp);

    public static void Walk(DataSeries s1, Action<int> onBar) { Walk(Lists.Create(s1), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, Action<int> onBar) { Walk(Lists.Create(s1, s2), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, DataSeries s3, Action<int> onBar) { Walk(Lists.Create(s1, s2, s3), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, DataSeries s3, DataSeries s4, Action<int> onBar) { Walk(Lists.Create(s1, s2, s3, s4), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, DataSeries s3, DataSeries s4, DataSeries s5, Action<int> onBar) { Walk(Lists.Create(s1, s2, s3, s4, s5), onBar); }

    public static void Walk(IEnumerable<DataSeries> series, Action<int> onBar)
    {
      int lastLength = -1;
      foreach (var ds in series)
      {
        if (ds.IsWalking)
          throw new InvalidOperationException("DataSeries is already being walked.");
        if (ds.OwnerThread != null && ds.OwnerThread != Thread.CurrentThread)
          throw new InvalidOperationException("DataSeries can only be walked on its owner thread.");
        if (lastLength >= 0 && ds.Length != lastLength)
          throw new InvalidOperationException("All series to be walked must be the same length.");
        if (lastLength == -1)
          lastLength = ds.Length;
      }

      foreach (var ds in series)
        ds.IsWalking = true;

      try
      {
        for (int i = 0; i < lastLength; i++)
        {
          foreach (var ds in series)
            ds._Pos = i;
          onBar(i);
        }
      }
      finally
      {
        foreach (var ds in series)
          ds.IsWalking = false;
      }
    }
  }

  public class LookedBackTooFarException : Exception { }

  public class DataSeries<T> : DataSeries, IEnumerable<T>
    where T : DataSeriesElement
  {
    readonly T[] _Elements;
    public DataSeries(string symbol, IEnumerable<T> elements)
      : this(symbol, elements.ToArray())
    {
    }

    public DataSeries(string symbol, T[] elements)
      : base(symbol)
    {
      _Elements = elements;
    }

    public DataSeries(string symbol, T[] elements, Thread ownerThread)
      : base(symbol, ownerThread)
    {
      _Elements = elements;
    }

    public DataSeries<T> Clone()
    {
      return new DataSeries<T>(Symbol, _Elements);
    }

    public DataSeries CloneForWorkerThread()
    {
      return new DataSeries<T>(Symbol, _Elements, null);
    }

    public T this[int offset]
    {
      get
      {
        if (!IsWalking)
          return _Elements[offset];
        else
        {
          if (offset < 0)
            throw new ArgumentException("Offset cannot be negative. Can\'t look forward.");
          var k = _Pos - offset;
          if (k < 0)
            throw new LookedBackTooFarException();
          return _Elements[k];
        }
      }
      set
      {
        if (!IsWalking)
          _Elements[offset] = value;
        else
        {
          if (offset < 0)
            throw new ArgumentException("Offset cannot be negative. Can\'t look forward.");
          var k = _Pos - offset;
          if (k < 0)
            throw new LookedBackTooFarException();
          _Elements[k] = value;
        }
      }
    }

    public DataSeries<TNewElement> MapElements<TNewElement>(Func<DataSeries<T>, DataSeries<TNewElement>, TNewElement> map)
      where TNewElement : DataSeriesElement, new()
    {
      TNewElement[] newInternalArray = new TNewElement[_Elements.Length];
      var result = new DataSeries<TNewElement>(Symbol, newInternalArray);
      Walk(this, result, pos => {
        TNewElement v;
        try
        {
          v = map(this, result);
        }
        catch (LookedBackTooFarException)
        {
          v = new TNewElement();
        }
        v.SetTimestamp(this[0].Timestamp);
        newInternalArray[pos] = v;
      });
      return result.SetTag(this.Tag);
    }

    public DataSeries<TNewElement> ZipElements<TOther, TNewElement>(DataSeries<TOther> other, Func<DataSeries<T>, DataSeries<TOther>, DataSeries<TNewElement>, TNewElement> map)
      where TOther : DataSeriesElement
      where TNewElement : DataSeriesElement
    {
      TNewElement[] newInternalArray = new TNewElement[_Elements.Length];
      var result = new DataSeries<TNewElement>(Symbol, newInternalArray);
      Walk(this, other, result, pos => {
        var v = map(this, other, result);
        v.SetTimestamp(this[0].Timestamp);
        newInternalArray[pos] = v;
      });
      return result;
    }

    public override DataSeries FromDate(DateTime timestamp)
    {
      return From(timestamp);
    }

    public DataSeries<T> From(DateTime timestamp)
    {
      return new DataSeries<T>(Symbol, _Elements.Where(x => x.Timestamp >= timestamp));
    }

    public DataSeries<T> To(DateTime timestamp)
    {
      return new DataSeries<T>(Symbol, _Elements.Where(x => x.Timestamp <= timestamp));
    }

    public DataSeries<T> From(string timestamp)
    {
      return From(DateTime.Parse(timestamp, null, DateTimeStyles.AdjustToUniversal));
    }

    public DataSeries<T> To(string timestamp)
    {
      return To(DateTime.Parse(timestamp, null, DateTimeStyles.AdjustToUniversal));
    }

    public IEnumerable<T> FromHere()
    {
      return _Elements.Skip(_Pos);
    }

    public DataSeries<T> SetTag(string tag)
    {
      Tag = tag;
      return this;
    }

    public IEnumerator<T> GetEnumerator()
    {
      return _Elements.Cast<T>().GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return (System.Collections.IEnumerator)GetEnumerator();
    }

    public override IEnumerable<DataSeriesElement> Elements { get { return _Elements; } }

    public override int Length { get { return _Elements.Length; } }
  }
}
