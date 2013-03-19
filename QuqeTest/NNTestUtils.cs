using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Generic;
using Quqe;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;

namespace QuqeTest
{
  static class NNTestUtils
  {
    public static PreprocessedData GetData(string startDate, string endDate)
    {
      return Versace.GetPreprocessedValues(PreprocessingType.Enhanced, "DIA", DateTime.Parse(startDate), DateTime.Parse(endDate),
        Versace.GetIdealSignalFunc(PredictionType.NextClose));
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
