using System;
using System.Collections.Generic;
using System.Linq;

namespace Quqe
{
  public static partial class Functions
  {
    public static Tuple2<TResult> CrossOver<T, TResult>(T[] a, T[] b, Func<T, T, Tuple2<T>> crossItems, Func<T[], TResult> makeResult)
    {
      var zipped = a.Zip(b, crossItems).ToArray();
      var newA = zipped.Select(x => x.Item1).ToArray();
      var newB = zipped.Select(x => x.Item2).ToArray();
      return new Tuple2<TResult>(makeResult(newA), makeResult(newB));
    }

    public static Tuple2<T> SelectTwoAccordingToQuality<T>(IList<T> items, Func<T, double> quality)
    {
      var possibleFirsts = items;
      var first = SelectOneAccordingToQuality(possibleFirsts, quality);
      var possibleSeconds = items.Except(Lists.Create(first)).ToList();
      var second = SelectOneAccordingToQuality(possibleSeconds, quality);
      return Tuple2.Create(first, second);
    }

    public static T SelectOneAccordingToQuality<T>(IList<T> items, Func<T, double> quality)
    {
      var qualitySum = items.Sum(quality);
      var spot = QuqeUtil.Random.NextDouble() * qualitySum;

      var a = 0.0;
      foreach (var item in items)
      {
        var b = a + quality(item);
        if (a <= spot && spot < b)
          return item;
        a = b;
      }
      throw new Exception("Your algorithm didn't work");
    }

    public static double Quantize(double v, double min, double step)
    {
      return Math.Round((v - min) / step) * step + min;
    }

    public static double RandomDouble(double min, double max)
    {
      return QuqeUtil.Random.NextDouble() * (max - min) + min;
    }
  }

  public class Tuple2<T> : IEnumerable<T>
  {
    public readonly T Item1;
    public readonly T Item2;

    public Tuple2(T item1, T item2)
    {
      Item1 = item1;
      Item2 = item2;
    }

    public IEnumerator<T> GetEnumerator()
    {
      return Lists.Create(Item1, Item2).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }

  public static class Tuple2
  {
    public static Tuple2<T> Create<T>(T item1, T item2) { return new Tuple2<T>(item1, item2); }
  }
}
