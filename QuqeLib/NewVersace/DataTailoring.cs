using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe.NewVersace
{
  public static class DataTailoring
  {
    public static Mat TailorInputs(Mat inputMatrix, int databaseAInputLength, Chromosome chrom)
    {
      var inputs = inputMatrix.Columns();

      // database selection
      if (chrom.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => x.SubVector(0, databaseAInputLength)).ToList();

      // complement coding
      if (chrom.UseComplementCoding)
        inputs = inputs.Select(DataPreprocessing.ComplementCode).ToList();

      // PCA
      if (chrom.UsePCA)
      {
        var principalComponents = DataPreprocessing.PrincipleComponents(inputs.ColumnsToMatrix());
        var pcNumber = Math.Min(chrom.PrincipalComponent, principalComponents.ColumnCount - 1);
        inputs = inputs.Select(x => DataPreprocessing.NthPrincipleComponent(principalComponents, pcNumber, x)).ToList();
      }

      return inputs.ColumnsToMatrix();
    }
  }
}