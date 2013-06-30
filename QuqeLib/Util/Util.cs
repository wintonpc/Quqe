using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quqe
{
  public static class Util
  {
    public static TValue GetDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key, Func<TKey, TValue> makeDefault)
    {
      TValue v;
      if (!d.TryGetValue(key, out v))
      {
        v = makeDefault(key);
        d[key] = v;
      }
      return v;
    }

    public static T As<T>(this object obj)
    {
      if (typeof(Enum).IsAssignableFrom(typeof(T)))
        return (T)Enum.ToObject(typeof(T), (int)Convert.ChangeType(obj, typeof(int)));
      if (obj is IConvertible)
        return (T)Convert.ChangeType(obj, typeof(T));
      return (T)(object)obj;
    }
  }
}
