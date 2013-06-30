using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using Quqe.Rabbit;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public static class MatrixExtensions
  {
    public static Mat ColumnsToMatrix(this IEnumerable<Vec> columns)
    {
      var cols = columns.ToIList();
      var m = cols.First().Count;
      var n = cols.Count;

      Mat X = new DenseMatrix(m, n);

      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = cols[j][i];

      return X;
    }

    public static List<Vec> Columns(this Mat m)
    {
      return Lists.Repeat(m.ColumnCount, j => m.Column(j));
    }

    public static List<Vec> Rows(this Mat m)
    {
      return Lists.Repeat(m.RowCount, j => m.Row(j));
    }

    public static Mat SeriesToMatrix(this IEnumerable<DataSeries<Value>> series)
    {
      var s = series.ToIList();
      var m = s.Count;
      var n = s.First().Length;

      Mat X = new DenseMatrix(m, n);
      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = s[i][j];

      return X;
    }
  }
}
