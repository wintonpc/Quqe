using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;

namespace Quqe
{
  public class GramSchmidtDecomposition
  {
    public readonly Matrix W;
    public readonly Matrix A;
    public GramSchmidtDecomposition(Matrix w, Matrix a)
    {
      W = w;
      A = a;
    }
  }

  public class RadialBasis
  {
    public readonly Vector Center;
    public readonly double Coefficient;
    public RadialBasis(Vector center, double coefficient)
    {
      Center = center;
      Coefficient = coefficient;
    }
  }

  public class RBFNetSolution
  {
    public readonly List<RadialBasis> Bases;
    public RBFNetSolution(List<RadialBasis> bases)
    {
      Bases = bases;
    }
  }

  public class RBFNet
  {
    public static GramSchmidtDecomposition GramSchmidt(Matrix P)
    {
      var n = P.RowCount;
      var m = P.ColumnCount;

      var W = new DenseMatrix(n, m);
      var A = new DenseMatrix(m, m);

      W.SetColumn(0, P.Column(0));
      A[0, 0] = 1;
      for (int k = 1; k < m; k++)
      {
        Vector projectionCorrection = new DenseVector(n);
        Vector Pk = (Vector)P.Column(k);
        for (int j = 0; j < k; j++)
        {
          Vector Wi = (Vector)W.Column(j);
          var Aik = Wi.DotProduct(Pk) / Wi.Norm(2);
          A[j, k] = Aik;
          projectionCorrection = (Vector)(projectionCorrection + Aik * Wi);
        }
        A[k, k] = 1;
        W.SetColumn(k, Pk - projectionCorrection);
      }
      return new GramSchmidtDecomposition(W, A);
    }

    public static Vector SolveRBFNet(Matrix H, Vector yHat, Action<Matrix, string> showMatrix)
    {
      showMatrix(H, "H");
      Matrix Ht = (Matrix)H.Transpose();
      showMatrix(Ht, "Ht");
      Matrix HtH = (Matrix)(Ht * H);
      showMatrix(HtH, "HtH");
      Matrix Ainv = (Matrix)HtH.Inverse();
      showMatrix(Ainv, "Ainv");
      Matrix proj = (Matrix)(Ainv * Ht);
      showMatrix(proj, "proj");
      return (Vector)(proj * yHat);
    }

    public static RBFNetSolution SolveRBFNet(Matrix P, Vector d, double errorTolerance)
    {
      var n = P.RowCount;
      var m = P.ColumnCount;

      Matrix A = new DenseMatrix(m, m);
      var candidates = P.Columns();
      var ws = new List<Vector>();
      var gs = new List<double>();

      for (int k = 0; k < m; k++)
      {
        var q = candidates.Select(pi => {
          var alphas = ws.Select(wj => wj.DotProduct(pi) / wj.Norm(2)).ToList();
          Vector wk;
          if (!alphas.Any())
            wk = pi;
          else
            wk = (Vector)(pi - ws.Zip(alphas, (wj, ajk) => new { wj = wj, ajk = ajk })
              .Aggregate((Vector)new DenseVector(n), (sum, z) => (Vector)(sum + z.ajk * z.wj)));
          var gk = wk.DotProduct(d) / wk.Norm(2);
          var err = Math.Pow(gk, 2) * wk.Norm(2) / d.Norm(2);
          return new { alphas = alphas, w = wk, g = gk, err = err };
        }).ToList();
        var zMaxErr = q.OrderByDescending(z => z.err).First();
        candidates.Remove(zMaxErr.w);
        ws.Add(zMaxErr.w);
        gs.Add(zMaxErr.g);
        for (int i = 0; i < zMaxErr.alphas.Count; i++)
          A[i, k] = zMaxErr.alphas[i];
        A[k, k] = 1;
      }
      Vector gHat = new DenseVector(gs.ToArray());
      var thetas = A.Inverse() * gHat;
      return new RBFNetSolution(ws.Zip(thetas, (w, t) => new RadialBasis(w, t)).ToList());
    }

    public static double Gaussian(double stdDev, double x)
    {
      return Math.Exp(-Math.Pow(x / stdDev, 2));
    }
  }
}
