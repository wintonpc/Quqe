using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Quqe
{
  public static class Pair
  {
    public static Pair<T> Cons<T>(this T head, Pair<T> tail)
    {
      return new Pair<T>(head, tail);
    }

    public static Pair<T> Cons<T>(this T head)
    {
      return new Pair<T>(head, null);
    }

    public static Pair<T> ToPairs<T>(this IEnumerable<T> items)
    {
      if (items == null || !items.Any())
        return null;
      else
        return items.First().Cons(items.Skip(1).ToPairs());
    }
  }

  /// <summary>
  /// A Lisp style cons cell. Useful for building lists recursively.
  /// </summary>
  [DebuggerDisplay("{DebuggerName,nq}")]
  public class Pair<T> : IEnumerable<T>
  {
    public readonly T Head;
    public readonly Pair<T> Tail;

    public Pair(T head, Pair<T> tail)
    {
      Head = head;
      Tail = tail;
    }

    string DebuggerName { get { return ToInitializerSyntax(); } }

    string ToInitializerSyntax()
    {
      if (this is Pair<string>)
        return "{ " + this.ToList().Join(", ", x => "\"" + x + "\"") + " }";
      else
        return "{ " + this.ToList().Join(", ") + " }";
    }

    public IEnumerator<T> GetEnumerator()
    {
      return new PairEnumerator(this);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return this.GetEnumerator();
    }

    class PairEnumerator : IEnumerator<T>
    {
      readonly Pair<T> _originalPair;
      Pair<T> _currentPair;

      public PairEnumerator(Pair<T> pair)
      {
        _originalPair = default(T).Cons(pair); // cons a dummy. the first call to MoveNext() after a Reset() will remove it.
        Reset();
      }

      public void Reset()
      {
        _currentPair = _originalPair;
      }

      public T Current
      {
        get { return _currentPair.Head; }
      }

      public void Dispose()
      {
      }

      object System.Collections.IEnumerator.Current
      {
        get { return this.Current; }
      }

      public bool MoveNext()
      {
        if (_currentPair.Tail == null)
          return false;

        _currentPair = _currentPair.Tail;
        return true;
      }
    }
  }

  // unfortunate that we need this... override LINQ's common IEnumerable extensions
  // to work properly with null pairs.
  // PCW: I tried a sentinel null value approach but found it to be a leaky abstraction.
  public static class PairExtensions
  {
    public static List<T> ToList<T>(this Pair<T> pair)
    {
      return pair == null ? new List<T>() : Enumerable.ToList(pair);
    }

    public static T[] ToArray<T>(this Pair<T> pair)
    {
      return pair == null ? new T[0] : Enumerable.ToArray(pair);
    }

    public static IEnumerable<T> Where<T>(this Pair<T> pair, Func<T, bool> predicate)
    {
      return pair == null ? new T[0] : Enumerable.Where(pair, predicate);
    }

    public static IEnumerable<TResult> Select<T, TResult>(this Pair<T> pair, Func<T, TResult> selector)
    {
      return pair == null ? new TResult[0] : Enumerable.Select(pair, selector);
    }

    public static IEnumerable<TResult> SelectMany<TSource, TResult>(this Pair<TSource> pair, Func<TSource, IEnumerable<TResult>> selector)
    {
      return pair == null ? new TResult[0] : Enumerable.SelectMany(pair, selector);
    }

    public static bool All<T>(this Pair<T> pair, Func<T, bool> predicate)
    {
      return pair == null ? true : Enumerable.All(pair, predicate);
    }

    public static bool Any<T>(this Pair<T> pair, Func<T, bool> predicate)
    {
      return pair == null ? false : Enumerable.Any(pair, predicate);
    }

    public static bool Any<T>(this Pair<T> pair)
    {
      return pair == null ? false : Enumerable.Any(pair);
    }

    public static int Count<T>(this Pair<T> pair)
    {
      return pair == null ? 0 : Enumerable.Count(pair);
    }

    public static int Count<T>(this Pair<T> pair, Func<T, bool> predicate)
    {
      return pair == null ? 0 : Enumerable.Count(pair, predicate);
    }
  }
}
