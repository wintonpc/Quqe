using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Quqe
{
  public static class QUtil
  {
    public static string DoublesToBase64(IEnumerable<double> h)
    {
      using (var ms = new MemoryStream())
      using (var bw = new BinaryWriter(ms))
      {
        foreach (var d in h)
          bw.Write(d);
        return Convert.ToBase64String(ms.ToArray());
      }
    }

    public static List<double> DoublesFromBase64(string b)
    {
      var result = new List<double>();
      using (var ms = new MemoryStream(Convert.FromBase64String(b)))
      using (var br = new BinaryReader(ms))
      {
        while (ms.Position < ms.Length)
          result.Add(br.ReadDouble());
      }
      return result;
    }
  }
}
