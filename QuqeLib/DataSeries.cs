using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using PCW;

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
  }

  public class Value : DataSeriesElement
  {
    public readonly double Val;
    public Value(DateTime timestamp, double value)
      : base(timestamp)
    {
      Val = value;
    }

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
    public abstract int Length { get; }
    Stack<Frame> Frames = new Stack<Frame>();

    protected bool IsFramed { get { return Frames.Any(); } }
    public int Pos { get { return IsFramed ? Frames.Peek().Pos : 0; } }

    public abstract IEnumerable<DataSeriesElement> Elements { get; }

    public DataSeries(string symbol)
    {
      Symbol = symbol;
    }

    void PushFrame(Frame f)
    {
      Frames.Push(f);
    }

    void PopFrame(Frame f)
    {
      Debug.Assert(Frames.Peek() == f);
      Frames.Pop();
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
            throw new ArgumentException("Offset is too large. DataSeries doesn't go back that far.");
          return _Elements[k];
        }
      }
    }

    public DataSeries<TNewElement> MapElements<TNewElement>(Func<DataSeries<T>, DataSeries<TNewElement>, TNewElement> map)
      where TNewElement : DataSeriesElement
    {
      TNewElement[] newInternalArray = new TNewElement[_Elements.Length];
      var result = new DataSeries<TNewElement>(Symbol, newInternalArray);
      Walk(this, result, pos => {
        var v = map(this, result);
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
      return new DataSeries<Bar>(symbol, Data.LoadNinjaBars(@"c:\users\wintonpc\git\Backtester\Share\" + symbol + ".txt"));
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
