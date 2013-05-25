using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Generic;
using Quqe;
using Quqe.NewVersace;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;

namespace QuqeTest
{
  static class NNTestUtils
  {
    public static DataSet GetData(string startDate, string endDate)
    {
      return DataPreprocessing.MakeTrainingSet("DIA", DateTime.Parse(startDate), DateTime.Parse(endDate), Signals.NextClose);
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
