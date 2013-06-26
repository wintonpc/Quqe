using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe.Rabbit
{
  static class List
  {
    public static void Repeat(int n, Action<int> f)
    {
      for (int i = 0; i < n; i++)
        f(i);
    }
    public static List<T> Repeat<T>(int n, Func<T> f)
    {
      List<T> result = new List<T>();
      for (int i = 0; i < n; i++)
        result.Add(f());
      return result;
    }
    public static List<T> Repeat<T>(int n, Func<int, T> f)
    {
      List<T> result = new List<T>();
      for (int i = 0; i < n; i++)
        result.Add(f(i));
      return result;
    }

    public static T Iterate<T>(int n, T arg0Init, Func<T, T> f)
    {
      var arg0 = arg0Init;
      for (int i = 0; i < n; i++)
        arg0 = f(arg0);
      return arg0;
    }

    public static T Iterate<T>(int n, T arg0Init, Func<int, T, T> f)
    {
      var arg0 = arg0Init;
      for (int i = 0; i < n; i++)
        arg0 = f(i, arg0);
      return arg0;
    }

    public static bool Equal<T>(IEnumerable<T> a, IEnumerable<T> b)
    {
      var ae = a.GetEnumerator();
      var be = b.GetEnumerator();
      while (ae.MoveNext())
        if (!be.MoveNext() || !object.Equals(ae.Current, be.Current))
          return false;
      if (be.MoveNext())
        return false;
      return true;
    }

    public static IList<T> ToIList<T>(this IEnumerable<T> xs)
    {
      return (xs as IList<T>) ?? xs.ToArray();
    }

    public static string Join<T>(this IEnumerable<T> items, string delimiter, Func<T, object> convert = null)
    {
      return string.Join(delimiter, convert == null ? items.Cast<object>().ToArray() : items.Select(convert).ToArray());
    }

    public static IEnumerable<T> Interleave<T>(this IEnumerable<T> items, IEnumerable<T> other)
    {
      var a = items.ToList();
      var b = other.ToList();
      var min = Math.Min(a.Count, b.Count);
      var max = Math.Max(a.Count, b.Count);
      var longer = a.Count > b.Count ? a : b;
      for (int i = 0; i < min; i++)
      {
          yield return a[i];
          yield return b[i];
      }
      for (int i = min; i < max; i++)
        yield return longer[i];
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> items)
    {
      return Shuffle(items, Random);
    }

    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> items, Random random)
    {
      var list = items.ToList();
      while (list.Any())
      {
        var item = list.RandomItem(random);
        list.Remove(item);
        yield return item;
      }
    }

    public static List<List<T>> PartitionByIndex<T>(this IEnumerable<T> list, params int[] indices)
    {
      var result = new List<List<T>>();
      for (int i = 0; i < indices.Length - 1; i++)
        result.Add(list.Skip(indices[i]).Take(indices[i + 1] - indices[i]).ToList());
      return result;
    }

    public static List<T> Create<T>(params T[] items)
    {
      return new List<T>(items);
    }

    static Random Random = new Random();
    public static T RandomItem<T>(this IEnumerable<T> items)
    {
      return items.RandomItem(Random);
    }

    public static T RandomItem<T>(this IEnumerable<T> items, Random random)
    {
      return items.ElementAt(random.Next(items.Count()));
    }

    public static IEnumerable<TJoined> Mesh<T, TListObj, TValue, TJoined>(IEnumerable<TListObj> listObjs, Func<TListObj, IEnumerable<T>> getList, Func<T, TValue> getVal, Func<TValue, TValue> increment, Func<IEnumerable<TListObj>, IEnumerable<T>, TJoined> join)
      where TListObj: class
      where TValue : IComparable
    {
      var minVal = listObjs.SelectMany(getList).Select(x => getVal(x)).Min();
      var maxVal = listObjs.SelectMany(getList).Select(x => getVal(x)).Max();

      var info = listObjs.Select(l => {
        var list = getList(l);
        return new { ListObj = l, First = getVal(list.First()), Last = getVal(list.Last()), Enumerator = new PeekableEnumerator<T>(list) };
      }).ToList();

      //Func<TListObj, TValue, bool> listContains = (l, v) => {
      //  var lInfo = info.First(i => i.ListObj.Equals(l));
      //  return lInfo.First.CompareTo(v) != 1 && lInfo.Last.CompareTo(v) != -1;
      //};

      //var nextVal = minVal;
      //while (nextVal.CompareTo(maxVal) != 1)
      //{
      //  var active = listObjs.Where(l => listContains(l, nextVal));
      //  yield return join(active, active.Select(l => {
      //    var en = info.First(i => i.ListObj.Equals(l)).Enumerator;
      //    en.MoveNext();
      //    return en.Current;
      //  }).ToList());
      //  nextVal = increment(nextVal);
      //}

      var nextVal = minVal;
      while (nextVal.CompareTo(maxVal) != 1)
      {
        var active = info.Where(i => !i.Enumerator.ReachedEnd && getVal(i.Enumerator.Peek()).CompareTo(nextVal) == 0);
        yield return join(active.Select(i => i.ListObj).ToList(), active.Select(i => i.Enumerator.Pop()).ToList());
        nextVal = increment(nextVal);
      }
    }

    class PeekableEnumerator<T>
    {
      List<T> List;
      int Pos;
      public PeekableEnumerator(IEnumerable<T> list)
      {
        List = list.ToList();
        Pos = 0;
      }

      public T Peek() { return List[Pos]; }
      public T Pop() { var x = List[Pos]; Pos++; return x; }
      public bool ReachedEnd { get { return Pos >= List.Count; } }
    }

    public static List<List<T>> Slice<T>(this IEnumerable<T> list, int numSlices)
    {
      var result = new List<List<T>>();
      var ls = list.ToList();
      var sliceSize = ls.Count / numSlices;
      for (int i = 0; i < numSlices -1; i++)
        result.Add(ls.Skip(i * sliceSize).Take(sliceSize).ToList());
      result.Add(ls.Skip((numSlices - 1) * sliceSize).ToList());
      return result;
    }

    public static void AddRange<T>(this ICollection<T> list, IEnumerable<T> range)
    {
      foreach (var x in range)
        list.Add(x);
    }

    public static void AddRange(this System.Collections.IList list, System.Collections.IEnumerable range)
    {
      foreach (var x in range)
        list.Add(x);
    }
  }
}
