using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;

namespace Quqe
{
  static class TestHelperHelpers
  {
    public static object MissingValue = new object();

    public static string Stringify(object obj, bool useFullTypeNames = false)
    {
      if (obj == null)
        return "null";
      else if (obj == MissingValue)
        return "(missing)";
      else if (obj is char)
        return string.Format("'{0}'", obj);
      else if (obj is string)
        return string.Format("\"{0}\"", obj);
      else if (obj is Enum)
        return string.Format("{0}.{1}", obj.GetType().Name, obj);
      else if (obj is System.Collections.IEnumerable)
      {
        var count = ((System.Collections.IEnumerable)obj).Cast<object>().Count();
        return string.Format("IEnumerable:{0} ({1})", count, Stringify(obj.GetType()));
      }
      else if (obj is Type)
      {
        var t = (Type)obj;
        if (!t.IsGenericType)
          return useFullTypeNames ? t.FullName : t.Name;
        else
        {
          var gDef = t.GetGenericTypeDefinition();
          var gArgs = t.GetGenericArguments();
          var unmangledName = gDef.Name.Split('`').First();
          return string.Format("{0}<{1}>", unmangledName, gArgs.Select(x => Stringify(x)).Join(", "));
        }
      }
      else if (obj.GetType().IsValueType)
        return string.Format("{0} ({1})", obj, obj.GetType().Name);
      else
        return Stringify(obj.GetType(), true);
    }
  }
}
