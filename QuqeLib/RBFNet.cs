using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;
using System.Diagnostics;

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
  public class QRDecomposition
  {
    public readonly Matrix Q;
    public readonly Matrix R;
    public QRDecomposition(Matrix q, Matrix r)
    {
      Q = q;
      R = r;
    }
  }

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

    public static QRDecomposition QRDecomposition(Matrix A)
    {
      var n = A.RowCount;
      var m = A.ColumnCount;

      Matrix Q = new DenseMatrix(n, m);
      Matrix R = new DenseMatrix(m, m);

      Q.SetColumn(0, A.Column(0).Normalize(2));
      R[0, 0] = Q.Column(0).DotProduct(A.Column(0));
      for (int k = 1; k < m; k++)
      {
        Vector Ak = (Vector)A.Column(k);
        Vector correlation = (Vector)Ak.CreateVector(Ak.Count);
        for (int i = 0; i < k; i++)
        {
          var Qi = Q.Column(i);
          double Rik = Ak.DotProduct(Qi);
          R[i, k] = Rik;
          correlation = (Vector)(correlation + Rik * Qi);
        }
        Vector Qk = (Vector)(Ak - correlation).Normalize(2);
        R[k, k] = Qk.DotProduct(Ak);
        Q.SetColumn(k, Qk);
      }
      return new QRDecomposition(Q, R);
    }

    public static Vector SolveRBFNet(Matrix H, Vector yHat, Action<Matrix, string> showMatrix = null)
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

    public static RBFNetSolution SolveRBFNet(Matrix P, List<Vector> centerVectors, Vector d, double errorTolerance)
    {
      P = Versace.MatrixFromColumns(P.Columns().Skip(1).ToList()); // !!
      //P = Versace.MatrixFromColumns(P.Columns().Select(v => (Vector)v.Normalize(2)).ToList()); // !!
      //P = Versace.MatrixFromColumns(P.Columns().Select(v => (Vector)v.Subtract(v.Average())).ToList()); // !!

      var n = P.RowCount;
      var m = P.ColumnCount;

      Matrix A = new DenseMatrix(m, m);
      var centers = new List<Vector>();

      // !!
      //centers.Add(null); // placeholder for the output bias weight

      centers.AddRange(centerVectors);
      var candidates = P.Columns().Zip(centers, (b, c) => new { Basis = b, Center = c }).ToList();
      var selectedCenters = new List<Vector>();
      var ws = new List<Vector>();
      var gs = new List<double>();

      //d = (Vector)d.Normalize(2);
      d = (Vector)d.Subtract(d.Average());

      double errSum = 0;
      for (int k = 0; k < m; k++)
      {
        var q = candidates.Select(candidate => {
          var pi = candidate.Basis;
          var alphas = ws.Select(wj => wj.DotProduct(pi) / wj.Norm(2)).ToList();
          Vector wk;
          if (!alphas.Any())
            wk = pi;
          else
            wk = (Vector)(pi - ws.Zip(alphas, (wj, ajk) => new { wj = wj, ajk = ajk })
              .Aggregate((Vector)new DenseVector(n), (sum, z) => (Vector)(sum + z.ajk * z.wj)));
          var gk = wk.DotProduct(d) / wk.Norm(2);
          var err = Math.Pow(gk, 2) * wk.Norm(2) / d.Norm(2);
          return new { alphas = alphas, w = wk, g = gk, err = err, candidate = candidate };
        }).ToList();
        var zMaxErr = q.OrderByDescending(z => z.err).First();
        errSum += zMaxErr.err;
        candidates.Remove(zMaxErr.candidate);
        ws.Add(zMaxErr.candidate.Basis);
        selectedCenters.Add(zMaxErr.candidate.Center);
        gs.Add(zMaxErr.g);
        for (int i = 0; i < zMaxErr.alphas.Count; i++)
          A[i, k] = zMaxErr.alphas[i];
        A[k, k] = 1;
      }

      //var weights = SolveRBFNet(

      //var allBases = selectedBases.ToList();
      //allBases.Insert(0, null);
      //var resultBases = allBases.Zip(weights, (b, w) => new RadialBasis(b != null ? b.Center : null, w)).ToList();
      //return new RBFNetSolution(resultBases);
      return null;
    }

    public static RBFNetSolution Solve(List<Vector> xs, List<double> ys, Func<Vector, Vector, double> phi, double tolerance)
    {
      var maxCenters = (int)(xs.Count / 3);
      var centerChoices = new HashSet<Vector>(xs);
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
      Trace.WriteLine("Total error: " + qualityTotal);
      Trace.WriteLine("Centers: ");
      foreach (var sb in selectedBases)
        Trace.WriteLine("  " + sb.Center);

      var weights = SolveRBFNet(
        Versace.MatrixFromColumns(new Vector[] { new DenseVector(n, 1) }.Concat(selectedBases.Select(sb => sb.Basis)).ToList()),
        new DenseVector(ys.ToArray()));

      var allBases = selectedBases.ToList();
      allBases.Insert(0, null);
      var resultBases = allBases.Zip(weights, (b, w) => new RadialBasis(b != null ? b.Center : null, w)).ToList();
      return new RBFNetSolution(resultBases);
    }

    // seems broken. not as good as Solve for the same number of centers. maybe orthogonalizing part is off somehow, or
    // normalization of the Q columns is interfering with the error quantity
    public static RBFNetSolution SolveQR(List<Vector> xs, List<double> ys, Func<Vector, Vector, double> phi, double tolerance)
    {
      var maxCenters = (int)(xs.Count / 3);
      var centerChoices = new HashSet<Vector>(xs);
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
      var selectedCenters = new List<Vector>();
      double errSum = 0;

      Matrix Q = new DenseMatrix(n, m);
      Matrix R = new DenseMatrix(m, m);
      var gs = new List<double>();

      for (int k = 0; k < m; k++)
      {
        var best = candidateBases.Select(cb => {
          // calculate QR
          Vector p = cb.Basis;
          Vector correlation = (Vector)p.CreateVector(p.Count);
          var rs = new List<double>();
          for (int i = 0; i < k; i++)
          {
            var Qi = Q.Column(i);
            double Rik = p.DotProduct(Qi);
            rs.Add(Rik);
            correlation = (Vector)(correlation + Rik * Qi);
          }
          Vector Qk = (Vector)(p - correlation).Normalize(2);
          rs.Add(Qk.DotProduct(p));

          // calculate error
          double gk = Qk.DotProduct(d);
          double err = Math.Pow(gk / d.Norm(2), 2);

          return new { Candidate = cb, Qk = Qk, rs = rs, gk = gk, err = err };
        }).OrderByDescending(z => z.err).First();

        candidateBases.Remove(best.Candidate);
        selectedCenters.Add(best.Candidate.Center);
        errSum += best.err;
        gs.Add(best.gk);
        Q.SetColumn(k, best.Qk);
        for (int i = 0; i <= k; i++)
          R[i, k] = best.rs[i];
        if (1 - errSum < tolerance)
          break;
      }
      Trace.WriteLine("Total error: " + errSum);

      m = selectedCenters.Count;
      Q = (Matrix)Q.SubMatrix(0, n, 0, m);
      R = (Matrix)R.SubMatrix(0, m, 0, m);

      Vector weights = (Vector)(R.Inverse() * Q.Transpose() * dOrig);
      return new RBFNetSolution(selectedCenters.Zip(weights, (c, w) => new RadialBasis(c, w)).ToList());
    }

    public static QRDecomposition QR2(Matrix P)
    {
      var candidateBases = P.Columns().Select(pi => new { Center = new DenseVector(1), Basis = pi }).ToList();
      var selectedCenters = new List<Vector>();
      double errSum = 0;

      var n = P.RowCount;
      var m = P.ColumnCount;

      Vector d = new DenseVector(n, 1);

      Matrix Q = new DenseMatrix(n, m);
      Matrix R = new DenseMatrix(m, m);

      for (int k = 0; k < m; k++)
      {
        var best = candidateBases.Select(cb => {
          // calculate QR
          Vector p = cb.Basis;
          Vector correlation = (Vector)p.CreateVector(p.Count);
          var rs = new List<double>();
          for (int i = 0; i < k; i++)
          {
            var Qi = Q.Column(i);
            double Rik = p.DotProduct(Qi);
            rs.Add(Rik);
            correlation = (Vector)(correlation + Rik * Qi);
          }
          Vector Qk = (Vector)(p - correlation).Normalize(2);
          rs.Add(Qk.DotProduct(p));

          // calculate error
          double gk = Qk.DotProduct(d.Normalize(2));
          double err = Math.Pow(gk, 2);

          return new { Candidate = cb, Qk = Qk, rs = rs, gk = gk, err = err };
        }).OrderByDescending(z => z.err).First();

        candidateBases.Remove(best.Candidate);
        selectedCenters.Add(best.Candidate.Center);
        errSum += best.err;
        Q.SetColumn(k, best.Qk);
        for (int i = 0; i <= k; i++)
          R[i, k] = best.rs[i];
      }

      return new QRDecomposition(Q, R);
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
  }
}
