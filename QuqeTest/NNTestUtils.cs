using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Quqe;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;

namespace QuqeTest
{
  static class NNTestUtils
  {
    public static DataSet GetData(string startDate, string endDate)
    {
      return DataPreprocessing.LoadTrainingSetFromDisk("DIA", DateTime.Parse(startDate, null, DateTimeStyles.AdjustToUniversal), DateTime.Parse(endDate, null, DateTimeStyles.AdjustToUniversal),
        Signals.NextClose);
    }

    public static string Checksum(Vec v)
    {
      var ms = new MemoryStream();
      var bw = new BinaryWriter(ms);
      foreach (var x in v)
        bw.Write(x);
      using (var md5 = MD5.Create())
        return Convert.ToBase64String(md5.ComputeHash(ms.ToArray()));
    }
  }
}
