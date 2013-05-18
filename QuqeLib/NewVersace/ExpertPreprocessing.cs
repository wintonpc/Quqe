using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe.NewVersace
{
  public static class ExpertPreprocessing
  {
    public static Tuple<Mat, Vec> PrepareData(TrainingSeed tSeed, Chromosome chrom)
    {
      return new Tuple<Mat, Vec>(PreprocessInputs(tSeed.Input, chrom, tSeed.DatabaseAInputLength),
                                 tSeed.Output);
    }

    static Mat PreprocessInputs(Mat inputMatrix, Chromosome chrom, int databaseAInputLength)
    {
      var inputs = inputMatrix.Columns();

      // database selection
      if (chrom.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => x.SubVector(0, databaseAInputLength)).ToList();

      // complement coding
      if (chrom.UseComplementCoding)
        inputs = inputs.Select(Preprocessing.ComplementCode).ToList();

      // PCA
      if (chrom.UsePCA)
      {
        var principalComponents = Preprocessing.PrincipleComponents(inputs.ColumnsToMatrix());
        var pcNumber = Math.Min(chrom.PrincipalComponent, principalComponents.ColumnCount - 1);
        inputs = inputs.Select(x => Preprocessing.NthPrincipleComponent(principalComponents, pcNumber, x)).ToList();
      }

      return inputs.ColumnsToMatrix();
    }
  }
}