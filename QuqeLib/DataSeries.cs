using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using PCW;
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

  public abstract class DataSeries
  {
    public readonly string Symbol;
    public string Tag { get; protected set; }
    public abstract int Length { get; }

    [ThreadStatic]
    static Dictionary<DataSeries, Stack<Frame>> ThreadFrames;

    protected bool IsFramed { get { return ThreadFrames != null && ThreadFrames.ContainsKey(this); } }
    public int Pos
    {
      get
      {
        if (!IsFramed)
          throw new InvalidOperationException("Pos can only be accessed when the DataSet is framed.");

        return ThreadFrames[this].Peek().Pos;
      }
    }

    public abstract IEnumerable<DataSeriesElement> Elements { get; }

    public DataSeries(string symbol)
    {
      Symbol = symbol;
    }

    void PushFrame(Frame f)
    {
      if (ThreadFrames == null)
        ThreadFrames = new Dictionary<DataSeries, Stack<Frame>>();

      Stack<Frame> frames;
      if (!ThreadFrames.TryGetValue(this, out frames))
      {
        frames = new Stack<Frame>();
        ThreadFrames.Add(this, frames);
      }

      frames.Push(f);
    }

    void PopFrame(Frame f)
    {
      Debug.Assert(ThreadFrames != null);

      var frames = ThreadFrames[this];

      Debug.Assert(frames.Peek() == f);

      frames.Pop();

      if (!frames.Any())
      {
        ThreadFrames.Remove(this);
        if (!ThreadFrames.Any())
          ThreadFrames = null;
      }
    }

    class Frame : IDisposable
    {
      public int Pos { get; private set; }
      readonly DataSeries[] Series;
      readonly int Length;
      public Frame(params DataSeries[] series)
      {
        Series = series;
        Length = series.Min(s => s.Length);
        Debug.Assert(Length == series.Max(s => s.Length));
        foreach (var s in Series)
          s.PushFrame(this);
      }

      bool IsDisposed;
      public void Dispose()
      {
        if (IsDisposed) return;
        IsDisposed = true;
        foreach (var s in Series)
          s.PopFrame(this);
      }

      public bool Advance()
      {
        if (Pos == Length - 1)
          return false;
        else
        {
          Pos++;
          return true;
        }
      }
    }

    public static void Walk(DataSeries s1, Action<int> onBar) { Walk(List.Create(s1), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, Action<int> onBar) { Walk(List.Create(s1, s2), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, DataSeries s3, Action<int> onBar) { Walk(List.Create(s1, s2, s3), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, DataSeries s3, DataSeries s4, Action<int> onBar) { Walk(List.Create(s1, s2, s3, s4), onBar); }
    public static void Walk(DataSeries s1, DataSeries s2, DataSeries s3, DataSeries s4, DataSeries s5, Action<int> onBar) { Walk(List.Create(s1, s2, s3, s4, s5), onBar); }

    public static void Walk(IEnumerable<DataSeries> series, Action<int> onBar)
    {
      using (var f = new Frame(series.ToArray()))
      {
        do
        {
          onBar(f.Pos);
        } while (f.Advance());
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

    public DataSeries<T> Clone()
    {
      return new DataSeries<T>(Symbol, _Elements);
    }

    public T this[int offset]
    {
      get
      {
        if (!IsFramed)
          return _Elements[offset];
        else
        {
          if (offset < 0)
            throw new ArgumentException("Offset cannot be negative. Can\'t look forward.");
          var k = Pos - offset;
          if (k < 0)
            throw new LookedBackTooFarException();
          return _Elements[k];
        }
      }
      set
      {
        if (!IsFramed)
          _Elements[offset] = value;
        else
        {
          if (offset < 0)
            throw new ArgumentException("Offset cannot be negative. Can\'t look forward.");
          var k = Pos - offset;
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
      return result;
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
      return From(DateTime.Parse(timestamp));
    }

    public DataSeries<T> To(string timestamp)
    {
      return To(DateTime.Parse(timestamp));
    }

    public IEnumerable<T> FromHere()
    {
      return _Elements.Skip(Pos);
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


  public static class Data
  {
    public static DataSeries<Bar> Load(string symbol)
    {
      return new DataSeries<Bar>(symbol, Data.LoadNinjaBars(@"c:\users\wintonpc\git\Quqe\Share\" + symbol + ".txt"));
    }

    public static List<Bar> LoadNinjaBars(string fn)
    {
      return File.ReadAllLines(fn).Select(line => {
        var toks = line.Trim().Split(';');
        return new Bar(
          DateTime.ParseExact(toks[0], "yyyyMMdd", null),
          double.Parse(toks[1]),
          double.Parse(toks[3]),
          double.Parse(toks[2]),
          double.Parse(toks[4]),
          long.Parse(toks[5]));
      }).ToList();
    }

    static List<DataSeries<Bar>> Series = new List<DataSeries<Bar>>();
    public static DataSeries<Bar> Get(string symbol)
    {
      var s = Series.FirstOrDefault(x => x.Symbol == symbol);
      if (s == null)
      {
        s = Load(symbol);
        Series.Add(s);
      }
      return s;
    }
  }
}
