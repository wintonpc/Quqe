using Machine.Specifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Quqe
{
  public static class TestHelpers
  {
    public static void ShouldThrow<TException>(this Action f, Action<TException> validateException = null)
      where TException : Exception
    {
      var exception = Catch.Exception(f);
      exception.ShouldNotBeNull();
      exception.ShouldBeOfType<TException>();
      if (validateException != null)
        validateException((TException)exception);
    }

    public static void ShouldEnumerateLike(this System.Collections.IEnumerable actual, System.Collections.IEnumerable expected)
    {
      if (actual == null)
        Reject("Actual");

      if (expected == null)
        Reject("Expected");

      var ae = actual.GetEnumerator();
      var ee = expected.GetEnumerator();

      int index = 0;
      while (true)
      {
        var aMore = ae.MoveNext();
        var eMore = ee.MoveNext();

        if (!aMore && !eMore)
          return;

        if (aMore && !eMore)
          Reject("Actual", ae.Current, "Expected", index);

        if (!aMore && eMore)
          Reject("Expected", ee.Current, "Actual", index);

        if (ae.Current == null && ee.Current == null)
          break;

        if (ae.Current == null)
          Reject(ae.Current, ee.Current, index);

        if (!ae.Current.Equals(ee.Current))
          Reject(ae.Current, ee.Current, index);

        index++;
      }
    }

    static void Reject(string badName)
    {
      throw new SpecificationException(string.Format("{0} is null", badName));
    }

    static void Reject(string badName, object badValue, string goodName, int index)
    {
      throw new SpecificationException(string.Format("{0} contains more items than {1}. First non-matching {0} item (at index {2}): {3}",
           badName, goodName, index, TestHelperHelpers.Stringify(badValue)));
    }

    static void Reject(object actual, object expected, int index)
    {
      throw new SpecificationException(string.Format("Enumerables differ at index {0}. Expected: {1}. Actual: {2}.",
        index, TestHelperHelpers.Stringify(expected), TestHelperHelpers.Stringify(actual)));
    }
  }
}
