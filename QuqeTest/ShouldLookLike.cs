using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Indigo.Util.Lisp;
using Machine.Specifications;
using List = PCW.List;
using PCW;

namespace Quqe
{
  public enum EnumerableComparison
  {
    /// <summary>Public fields and properties must match exactly</summary>
    Strict,
    /// <summary>
    /// Two IEnumerables may be in different order, as long as they both have the same number of items,
    /// and for each expected item there is an actual item that looks like it.
    /// </summary>
    IgnoreOrder,
    /// <summary>
    /// Like IgnoreOrder, except IEnumerables in the "actual" object may contain items not in the
    /// corresponding "expected" IEnumerable. (Thus, the "same number of items" requirement is relaxed.)
    /// </summary>
    AllowExtraActual
  }

  public class ShouldLookLikeOptions
  {
    public List<EnumerableOption> EnumerableOptions = new List<EnumerableOption>();
    public List<string> ExceptPaths = new List<string>();
  }

  public class EnumerableOption
  {
    /// <summary>
    /// When ItemType is set, this EnumerableOption pertains only to IEnumerables with an item type equal to,
    /// or derived from, ItemType.
    /// </summary>
    public Type ItemType = typeof(object);
    public EnumerableComparison Comparison = EnumerableComparison.Strict;
  }

  public static class ShouldLookLikeHelper
  {
    /// <summary>
    /// Asserts that the actual and expected arguments are similar. Similarity is determined by
    /// recursively comparing the publicly visible fields and properties, if any.
    /// </summary>
    public static void ShouldLookLike(this object actual, object expected, ShouldLookLikeOptions options = null)
    {
      options = options ?? new ShouldLookLikeOptions();
      Check(expected, actual, options, List.Create(".").ToPairs(), new HashSet<object>());
    }

    public static void ShouldNotLookLike(this object actual, object expected, ShouldLookLikeOptions options = null)
    {
      new Action(() => actual.ShouldLookLike(expected, options)).ShouldThrow<SpecificationException>();
    }

    public static bool LooksLike(this object actual, object expected, ShouldLookLikeOptions options = null)
    {
      return Catch.Exception(() => actual.ShouldLookLike(expected, options)) == null;
    }

    static void Check(object expected, object actual, ShouldLookLikeOptions options, Indigo.Util.Lisp.Pair<string> path, HashSet<object> visitedExpecteds)
    {
      if (actual == null && expected == null)
        return;

      if (actual == null || expected == null)
        Reject(expected, actual, path, options);

      if (visitedExpecteds.Contains(expected))
        return;
      visitedExpecteds.Add(expected);

      var type = actual.GetType();
      if (expected.GetType() != type)
        Reject(expected, actual, path, options);

      if (type.IsValueType || actual is string)
      {
        if (!actual.Equals(expected))
          Reject(expected, actual, path, options);
        return;
      }

      if (actual is System.Collections.IEnumerable)
      {
        var allItems = ToObjectList(expected).Concat(ToObjectList(actual)).Where(x => x != null).ToList();
        if (!allItems.Any())
          return;
        var concreteItemType = allItems.First().GetType();
        var enumOption = options.EnumerableOptions.FirstOrDefault(x => x.ItemType.IsAssignableFrom(concreteItemType)) ?? new EnumerableOption();

        if (enumOption.Comparison == EnumerableComparison.Strict)
          CheckEnumerableStrict(expected, actual, options, path, visitedExpecteds);
        else
          CheckEnumerableUnordered(expected, actual, options, enumOption, path, visitedExpecteds);
        return;
      }

      var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
      foreach (var fi in fields)
        Check(fi.GetValue(expected), fi.GetValue(actual), options, fi.Name.Cons(path), visitedExpecteds);

      var propsWithGetters = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
        .Select(pi => new { Prop = pi, Get = pi.GetGetMethod(false) }).Where(z => z.Get != null);
      foreach (var z in propsWithGetters)
      {
        object propExpected = null;
        object propActual = null;
        try
        {
          propExpected = z.Get.Invoke(expected, new object[0]);
          propActual = z.Get.Invoke(actual, new object[0]);
        }
        catch (Exception)
        {
          Reject(propExpected, propActual, z.Prop.Name.Cons(path), options);
          return;
        }
        Check(propExpected, propActual, options, z.Prop.Name.Cons(path), visitedExpecteds);
      }
    }

    static void CheckEnumerableStrict(object expected, object actual, ShouldLookLikeOptions options, Indigo.Util.Lisp.Pair<string> path, HashSet<object> visitedExpecteds)
    {
      var expectedObjects = ToObjectList(expected);
      var actualObjects = ToObjectList(actual);

      if (expectedObjects.Count != actualObjects.Count)
        Reject(expected, actual, path, options);

      var q = expectedObjects.Zip(actualObjects, (e, a) => new { Expected = e, Actual = a });
      int c = 0;
      foreach (var z in q)
      {
        Check(z.Expected, z.Actual, options, Bracketize(c).Cons(path), visitedExpecteds);
        c++;
      }
    }

    static string Bracketize(int n)
    {
      return string.Format("[{0}]", n);
    }

    static void CheckEnumerableUnordered(object expectedEnumerable, object actualEnumerable, ShouldLookLikeOptions options,
      EnumerableOption enumOption, Indigo.Util.Lisp.Pair<string> path, HashSet<object> visitedExpecteds)
    {
      var expectedObjects = ToObjectList(expectedEnumerable);
      var actualObjects = ToObjectList(actualEnumerable);

      var uncheckedExpected = expectedObjects.ToList();
      var uncheckedActual = actualObjects.ToList();

      while (true) // would look a lot better if C# had TCO. Then we could do this recursively.
      {
      ContinueWhile:
        if (!uncheckedExpected.Any())
          break;

        var expected = uncheckedExpected.First();
        if (uncheckedActual.Any()) // we have a chance of matching an actual with the expected and continuing to the next expected
        {
          foreach (var actual in uncheckedActual)
          {
            bool threw = false;
            try
            {
              var throwAwayHash = new HashSet<object>(); // we need to still guard against cycles, but don't remember anything we saw.
              Check(expected, actual, options, "?".Cons(path), throwAwayHash);
            }
            catch (SpecificationException)
            {
              threw = true;
            }
            if (!threw)
            {
              uncheckedExpected.Remove(expected);
              uncheckedActual.Remove(actual);
              goto ContinueWhile;
            }
          }
        }

        // didn't find a match :(
        Reject(expected, TestHelperHelpers.MissingValue, Bracketize(expectedObjects.IndexOf(expected)).Cons(path), options);
      }

      if (uncheckedActual.Any() && enumOption.Comparison == EnumerableComparison.IgnoreOrder)
      {
        var actual = uncheckedActual.First();
        Reject(TestHelperHelpers.MissingValue, actual, Bracketize(actualObjects.IndexOf(actual)).Cons(path), options);
      }
    }

    static List<object> ToObjectList(object iEnumerable)
    {
      return ((System.Collections.IEnumerable)iEnumerable).Cast<object>().ToList();
    }

    static void Reject(object expected, object actual, Indigo.Util.Lisp.Pair<string> path, ShouldLookLikeOptions options)
    {
      var pathString = path.Reverse().Join("/");

      if (options.ExceptPaths.Contains(pathString))
        return;

      throw new SpecificationException(string.Format("Mismatch at {0}\nExpected : {1}\nActual   : {2}",
        pathString, TestHelperHelpers.Stringify(expected), TestHelperHelpers.Stringify(actual)));
    }
  }
}
