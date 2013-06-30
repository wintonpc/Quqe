using System;
using System.Collections.Generic;
using System.IO;

namespace Quqe
{
  public class LittleEndian
  {
    public static string DoublesToBase64String(IEnumerable<double> xs)
    {
      var b = DoublesToBufferRegion(xs);
      return Convert.ToBase64String(b.Buffer, b.Offset, b.Length);
    }

    public static List<double> Base64StringToDoubles(string encoded)
    {
      var ms = new MemoryStream(Convert.FromBase64String(encoded));
      return MemoryStreamToDoubles(ms);
    }

    public static byte[] DoublesToBytes(IEnumerable<double> xs)
    {
      return DoublesToBufferRegion(xs).ToArray();
    }

    public static double[] BytesToDoubles(byte[] bs)
    {
      return MemoryStreamToDoubles(new MemoryStream(bs)).ToArray();
    }

    static BufferRegion DoublesToBufferRegion(IEnumerable<double> xs)
    {
      if (!BitConverter.IsLittleEndian)
        throw new InvalidOperationException("I expected to be run on a little endian architecture");

      var ms = new MemoryStream();
      using (var bw = new BinaryWriter(ms))
      {
        foreach (var x in xs)
          bw.Write(x);
        return new BufferRegion(ms);
      }
    }

    static List<double> MemoryStreamToDoubles(MemoryStream ms)
    {
      if (!BitConverter.IsLittleEndian)
        throw new InvalidOperationException("I expected to be run on a little endian architecture");

      var result = new List<double>();
      using (var br = new BinaryReader(ms))
      {
        while (ms.Position < ms.Length)
          result.Add(br.ReadDouble());
      }
      return result;
    }

    class BufferRegion
    {
      public readonly byte[] Buffer;
      public readonly int Offset;
      public readonly int Length;

      BufferRegion(byte[] buffer, int offset, int length)
      {
        Buffer = buffer;
        Offset = offset;
        Length = length;
      }

      public BufferRegion(MemoryStream ms)
        : this(ms.GetBuffer(), 0, (int)ms.Length) { }

      public byte[] ToArray()
      {
        var result = new byte[Length];
        Array.Copy(Buffer, Offset, result, 0, Length);
        return result;
      }
    }
  }
}
