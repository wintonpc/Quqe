using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;

namespace Quqe
{
  public class VersaceContext
  {
    public readonly VersaceSettings Settings;
    public readonly PreprocessedData Training;
    public readonly PreprocessedData Validation;
    public int DatabaseAInputLength { get { return Training.DatabaseAInputLength; } }
    public int DatabaseBInputLength { get { return Training.Input.RowCount; } }

    public VersaceContext(VersaceSettings settings, PreprocessedData training, PreprocessedData validation)
    {
      Settings = settings;
      Training = training;
      Validation = validation;
      Debug.Assert(training.DatabaseAInputLength == validation.DatabaseAInputLength);
    }
  }
}
