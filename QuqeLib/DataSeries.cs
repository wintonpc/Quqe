using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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
    public abstract IEnumerable<DataSeriesElement> GetElements();
    public int Pos { get; protected set; }

    public DataSeries(string symbol)
    {
      Symbol = symbol;
    }

    public static void Walk<T1, T2>(DataSeries<T1> s1, DataSeries<T2> s2, Action<DataSeries<T1>, DataSeries<T2>> f)
      where T1 : DataSeriesElement
      where T2 : DataSeriesElement
    {
      var c1 = new DataSeries<T1>(s1.Symbol, s1.GetElements().Cast<T1>());
      var c2 = new DataSeries<T2>(s2.Symbol, s2.GetElements().Cast<T2>());
      var count = c1.Count();
      for (int i = 0; i < count; i++)
      {
        c1.Pos = i;
        c2.Pos = i;
        f(c1, c2);
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
      Pos = 0;
      _Elements = elements;
    }

    DataSeries(string symbol, T[] elements, int pos)
      : base(symbol)
    {
      Pos = pos;
      _Elements = elements;
    }

    public T this[int offset]
    {
      get
      {
        if (offset < 0)
          throw new ArgumentException("Offset cannot be negative. Can\'t look forward.");
        var k = Pos - offset;
        if (k < 0)
          throw new ArgumentException("Offset it too large. DataSeries doesn't go back that far.");
        return _Elements[k];
      }
    }

    public DataSeries<TNewElement> MapElements<TNewElement>(Func<DataSeries<T>, DataSeries<TNewElement>, TNewElement> map)
      where TNewElement : DataSeriesElement
    {
      var newElements = new TNewElement[_Elements.Length];
      for (int i = 0; i < _Elements.Length; i++)
      {
        var clone = new DataSeries<T>(Symbol, _Elements, i);
        var v = map(clone, new DataSeries<TNewElement>(Symbol, newElements, i));
        v.SetTimestamp(clone[0].Timestamp);
        newElements[i] = v;
      }
      return new DataSeries<TNewElement>(Symbol, newElements);
    }

    public DataSeries<TNewElement> ZipElements<TNewElement>(DataSeries<T> other, Func<DataSeries<T>, DataSeries<T>, DataSeries<TNewElement>, TNewElement> map)
      where TNewElement : DataSeriesElement
    {
      if (this._Elements.Length != other._Elements.Length || this._Elements.First().Timestamp != other._Elements.First().Timestamp)
        throw new ArgumentException("DataSeries arguments must be the same length and start on the same date");
      var newElements = new TNewElement[_Elements.Length];
      for (int i = 0; i < _Elements.Length; i++)
      {
        var clone = new DataSeries<T>(Symbol, _Elements, i);
        var otherClone = new DataSeries<T>(Symbol, other._Elements, i);
        var v = map(clone, otherClone, new DataSeries<TNewElement>(Symbol, newElements, i));
        v.SetTimestamp(clone[0].Timestamp);
        newElements[i] = v;
      }
      return new DataSeries<TNewElement>(Symbol, newElements);
    }

    public DataSeries<T> From(DateTime timestamp)
    {
      return new DataSeries<T>(Symbol, _Elements.Where(x => x.Timestamp >= timestamp));
    }

    public DataSeries<T> To(DateTime timestamp)
    {
      return new DataSeries<T>(Symbol, _Elements.Where(x => x.Timestamp <= timestamp));
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
      return ((System.Collections.IEnumerable)_Elements).GetEnumerator();
    }

    public override IEnumerable<DataSeriesElement> GetElements()
    {
      return _Elements;
    }

    public T[] Elements { get { return _Elements; } }
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
