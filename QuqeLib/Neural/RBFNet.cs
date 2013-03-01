using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using MathNet.Numerics.LinearAlgebra.Double;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe
{
  public class RadialBasis
  {
    /// <summary>Null if output bias weight</summary>
    public readonly Vec Center;
    public readonly double Weight;
    public RadialBasis(Vec center, double weight)
    {
      Center = center;
      Weight = weight;
    }
  }

  public class RBFNetSolution
  {
    public bool IsDegenerate;
    public readonly List<RadialBasis> Bases;
    public RBFNetSolution(List<RadialBasis> bases, double recommendedSpread, bool isDegenerate)
    {
      Bases = bases;
      IsDegenerate = isDegenerate;
    }
  }

  public class RBFNet : IPredictor
  {
    List<RadialBasis> Bases;
    double OutputBias;
    public readonly double Spread;
    public bool IsDegenerate { get; private set; }
    public int NumCenters { get { return Bases.Count; } }

    RBFNet(List<RadialBasis> bases, double outputBias, double spread, bool isDegenerate)
    {
      Bases = bases;
      OutputBias = outputBias;
      Spread = spread;
      IsDegenerate = isDegenerate;
    }

    public static RBFNet Train(Mat trainingData, Vec outputData, double tolerance, double spread)
    {
      var solution = SolveOLS(trainingData.Columns(), outputData.ToList(), tolerance, spread); // TODO: don't call Columns
      return new RBFNet(solution.Bases.Where(b => b.Center != null).ToList(), solution.Bases.Single(b => b.Center == null).Weight, spread, solution.IsDegenerate);
    }

    static double Phi(Vec x, Vec c, double spread)
    {
      return Gaussian(spread, (x - c).Norm(2));
    }

    public double Propagate(Vec x)
    {
      return OutputBias + Bases.Sum(b => b.Weight * Phi(x, b.Center, Spread));
    }

    public static Vec SolveLS(Mat H, Vec yHat, Action<Mat, string> showMatrix = null)
    {
      if (showMatrix != null) showMatrix(H, "H");
      var Ht = H.Transpose();
      if (showMatrix != null) showMatrix(Ht, "Ht");
      var HtH = (Ht * H);
      if (showMatrix != null) showMatrix(HtH, "HtH");
      var Ainv = HtH.Inverse();
      if (showMatrix != null) showMatrix(Ainv, "Ainv");
      var proj = (Ainv * Ht);
      if (showMatrix != null) showMatrix(proj, "proj");
      return proj * yHat;
    }

    public static RBFNetSolution SolveOLS(List<Vec> xs, List<double> ys, double tolerance, double spread)
    {
      Func<Vec, Vec, double> phi = (x, c) => Phi(x, c, spread);
      var maxCenters = (int)Math.Min(xs.Count, Math.Max(1, 4 * Math.Sqrt(xs.Count)));
      var centers = new List<Vec>(xs);
      var n = xs.Count;
      var m = centers.Count;

      Mat P = new DenseMatrix(n, m);
      for (int i = 0; i < n; i++)
        for (int j = 0; j < m; j++)
          P[i, j] = phi(xs[i], centers[j]);

      Vec dOrig = new DenseVector(ys.ToArray());
      Vec d = dOrig.Subtract(dOrig.Average());
      Vec dNorm = d.Normalize(2);

      var candidateBases = centers.Zip(P.Columns(), (ci, pi) => new { Center = ci, Basis = pi }).ToList();
      var selectedBases = candidateBases.Select(cb => new { Center = cb.Center, Basis = cb.Basis, OrthoBasis = cb.Basis }).Take(0).ToList();
      double qualityTotal = 0;

      var context = QMCreateOrthoContext(n, m);
      var iters = m;
      for (int k = 0; k < iters; k++)
      {
        int stride = n;
        double[] selectedOrthonormalBases = new double[selectedBases.Count * stride];
        for (int ptr = 0, sb = 0; sb < selectedBases.Count; ptr += stride, sb++)
          Array.Copy(selectedBases[sb].OrthoBasis.ToArray(), 0, selectedOrthonormalBases, ptr, stride);

        var best = candidateBases.Select(cb => {
          var pi = cb.Basis;
          Vec w;
          if (!selectedBases.Any())
          {
            w = pi.Normalize(2);
          }
          else
          {
            double[] result = new double[pi.Count];
            double[] pis = pi.ToList().ToArray();
            double[] sobs = selectedOrthonormalBases.ToList().ToArray();
            QMOrthogonalize(context, pis, selectedBases.Count, sobs);
            w = new DenseVector(pis.ToList().ToArray());
          }
          //Vec wCheck = Orthogonalize(pi, selectedBases.Select(b => b.OrthoBasis).ToList()).Normalize(2);
          //var w = wCheck;
          //Debug.Assert(w.Subtract(wCheck).Norm(2) < 0.0001);
          var err = Math.Pow(w.DotProduct(dNorm), 2);
          return new { Center = cb.Center, OrthoBasis = w, Quality = err, Candidate = cb };
        }).OrderByDescending(q => q.Quality).First();
        candidateBases.Remove(best.Candidate);
        selectedBases.Add(new { Center = best.Center, Basis = best.Candidate.Basis, OrthoBasis = best.OrthoBasis });
        qualityTotal += best.Quality;
        if (1 - qualityTotal < tolerance)
          break;
      }
      QMDestroyOrthoContext(context);

      //Trace.WriteLine(string.Format("Centers: {0}, Spread: {1}, Tolerance: {2}", selectedBases.Count, spread, tolerance));

      var weights = SolveLS(
        new Vec[] { new DenseVector(n, 1) }.Concat(selectedBases.Select(sb => sb.Basis)).ColumnsToMatrix(),
        new DenseVector(ys.ToArray()));
      bool isDegenerate = false;
      if (weights.Any(w => double.IsNaN(w)))
      {
        Trace.WriteLine("! Degenerate RBF network !");
        isDegenerate = true;
      }
      var allBases = selectedBases.ToList();
      allBases.Insert(0, null);
      var resultBases = allBases.Zip(weights, (b, w) => new RadialBasis(b != null ? b.Center : null, w)).ToList();
      return new RBFNetSolution(resultBases, spread, isDegenerate);
    }

    [DllImport("QuqeMath.dll", EntryPoint = "CreateOrthoContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreateOrthoContext(int basisDimension, int maxBasisCount);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroyOrthoContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMDestroyOrthoContext(IntPtr context);

    [DllImport("QuqeMath.dll", EntryPoint = "Orthogonalize", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMOrthogonalize(IntPtr c, double[] p, int n, double[] orthonormalBases);

    public static Vec Orthogonalize(Vec pi, List<Vec> withRespectToNormalizedBases)
    {
      Vec result = pi;
      foreach (var v in withRespectToNormalizedBases)
        result = result - v.DotProduct(pi) * v;
      return result;
    }

    public static double Gaussian(double stdDev, double x)
    {
      return Math.Exp(-Math.Pow(x / stdDev, 2));
    }

    public double Predict(Vec input)
    {
      return IsDegenerate ? 0 : Propagate(input);
    }

    public IPredictor Reset() { return this; } // RBFs are stateless

    public void Dispose()
    {
      // nothing to do
    }
  }
}
