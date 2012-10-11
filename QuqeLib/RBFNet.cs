using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;
using System.Diagnostics;

namespace Quqe
{
  public class RadialBasis
  {
    /// <summary>Null if output bias weight</summary>
    public readonly Vector Center;
    public readonly double Weight;
    public RadialBasis(Vector center, double weight)
    {
      Center = center;
      Weight = weight;
    }
  }

  public class RBFNetSolution
  {
    public bool IsDegenerate;
    public readonly List<RadialBasis> Bases;
    public RBFNetSolution(List<RadialBasis> bases, bool isDegenerate)
    {
      Bases = bases;
      IsDegenerate = isDegenerate;
    }
  }

  public class RBFNet : IPredictor
  {
    List<RadialBasis> Bases;
    double OutputBias;
    double Spread;
    public static bool ShouldTrace = true;
    public bool IsDegenerate { get; private set; }

    RBFNet(List<RadialBasis> bases, double outputBias, double spread, bool isDegenerate)
    {
      Bases = bases;
      OutputBias = outputBias;
      Spread = spread;
      IsDegenerate = isDegenerate;
    }

    public static RBFNet Train(Matrix trainingData, Vector outputData, double spread, double tolerance)
    {
      var solution = SolveOLS(trainingData.Columns(), outputData.ToList(), (x, c) => Phi(x, c, spread), tolerance, spread_ForTraceOnly: spread);
      return new RBFNet(solution.Bases.Where(b => b.Center != null).ToList(), solution.Bases.Single(b => b.Center == null).Weight, spread, solution.IsDegenerate);
    }

    static double Phi(Vector x, Vector c, double spread)
    {
      return RBFNet.Gaussian(spread, (x - c).Norm(2));
    }

    public double Propagate(Vector<double> x)
    {
      return OutputBias + Bases.Sum(b => b.Weight * Phi((Vector)x, b.Center, Spread));
    }

    public static Vector SolveLS(Matrix H, Vector yHat, Action<Matrix, string> showMatrix = null)
    {
      if (showMatrix != null) showMatrix(H, "H");
      Matrix Ht = (Matrix)H.Transpose();
      if (showMatrix != null) showMatrix(Ht, "Ht");
      Matrix HtH = (Matrix)(Ht * H);
      if (showMatrix != null) showMatrix(HtH, "HtH");
      Matrix Ainv = (Matrix)HtH.Inverse();
      if (showMatrix != null) showMatrix(Ainv, "Ainv");
      Matrix proj = (Matrix)(Ainv * Ht);
      if (showMatrix != null) showMatrix(proj, "proj");
      return (Vector)(proj * yHat);
    }

    public static RBFNetSolution SolveOLS(List<Vector> xs, List<double> ys, Func<Vector, Vector, double> phi, double tolerance, double spread_ForTraceOnly = 0)
    {
      int retryCount = 0;
    Solve:
      //var maxCenters = Math.Max(Math.Min(xs.Count, 3), (int)(xs.Count / 3));
      var maxCenters = (int)Math.Min(xs.Count, Math.Max(1, 4 * Math.Sqrt(xs.Count)));
      var centerChoices = new List<Vector>(xs);
      var centers = List.Repeat(maxCenters, () => {
        var c = centerChoices.RandomItem();
        centerChoices.Remove(c);
        return c;
      });
      var n = xs.Count;
      var m = centers.Count;

      Matrix P = new DenseMatrix(n, m);
      for (int i = 0; i < n; i++)
        for (int j = 0; j < m; j++)
          P[i, j] = phi(xs[i], centers[j]);

      Vector dOrig = new DenseVector(ys.ToArray());
      Vector d = (Vector)dOrig.Subtract(dOrig.Average());

      var candidateBases = centers.Zip(P.Columns(), (ci, pi) => new { Center = ci, Basis = pi }).ToList();
      var selectedBases = candidateBases.Select(cb => new { Center = cb.Center, Basis = cb.Basis, OrthoBasis = cb.Basis }).Take(0).ToList();
      double qualityTotal = 0;

      var iters = m;
      for (int k = 0; k < iters; k++)
      {
        var best = candidateBases.Select(cb => {
          var pi = cb.Basis;
          Vector w = (Vector)Orthogonalize(pi, selectedBases.Select(sb => sb.OrthoBasis).ToList()).Normalize(2);
          var err = Math.Pow(w.DotProduct(d.Normalize(2)), 2);
          return new { Center = cb.Center, OrthoBasis = w, Quality = err, Candidate = cb };
        }).OrderByDescending(q => q.Quality).First();
        candidateBases.Remove(best.Candidate);
        selectedBases.Add(new { Center = best.Center, Basis = best.Candidate.Basis, OrthoBasis = best.OrthoBasis });
        qualityTotal += best.Quality;
        if (1 - qualityTotal < tolerance)
          break;
      }

      if (ShouldTrace)
        Trace.WriteLine(string.Format("Centers: {0}, Spread: {1}, Tolerance: {2}", selectedBases.Count, spread_ForTraceOnly, tolerance));

      var weights = SolveLS(
        Versace.MatrixFromColumns(new Vector[] { new DenseVector(n, 1) }.Concat(selectedBases.Select(sb => sb.Basis)).ToList()),
        new DenseVector(ys.ToArray()));
      bool isDegenerate = false;
      if (weights.Any(w => double.IsNaN(w)))
      {
        if (retryCount < 3)
        {
          retryCount++;
          goto Solve;
        }
        else
        {
          Trace.WriteLine("! Degenerate RBF network !");
          isDegenerate = true;
        }
      }
      var allBases = selectedBases.ToList();
      allBases.Insert(0, null);
      var resultBases = allBases.Zip(weights, (b, w) => new RadialBasis(b != null ? b.Center : null, w)).ToList();
      return new RBFNetSolution(resultBases, isDegenerate);
    }

    public static Vector Orthogonalize(Vector pi, List<Vector> withRespectTo)
    {
      Vector result = pi;
      foreach (var v in withRespectTo)
        result = (Vector)(result - v.DotProduct(pi) * v.Normalize(2));
      return result;
    }

    public static double Gaussian(double stdDev, double x)
    {
      return Math.Exp(-Math.Pow(x / stdDev, 2));
    }

    public double Predict(Vector<double> input)
    {
      return Propagate(input);
    }

    public void Reset() { }

    public System.Xml.Linq.XElement ToXml()
    {
      throw new NotImplementedException();
    }
  }
}
