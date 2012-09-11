using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;
using System.Diagnostics;

namespace Quqe
{
  public class BCOResult
  {
    public double[] MinimumLocation;
    public double MinimumValue;
    public List<Vector> Path;
  }

  public class BCO
  {
    static double BaseAngleMean = DegreesToRadians(62);
    static double BaseAngleStdDev = DegreesToRadians(26);

    public static BCOResult OptimizeWithParameterAdaptation(double[] initialLocation, Func<Vector, double> F,
      int itersPerEpoch, int initialPrecision, int finalPrecision)
    {
      var currentLocation = initialLocation;
      BCOResult result = null;
      for (int precision = initialPrecision; precision <= finalPrecision; precision++)
      {
        result = Optimize(currentLocation, F, itersPerEpoch, Math.Pow(10, -precision), 12, 10);
        currentLocation = result.MinimumLocation;
      }
      return result;
    }

    public static BCOResult Optimize(double[] initialLocation, Func<Vector, double> F, int maxIterations, double epsilon, int nc, int npc)
    {
      double T0 = Math.Pow(epsilon, 0.30) * Math.Pow(10, -1.73);
      double b = T0 * Math.Pow(T0, -1.54) /* * Math.Pow(10, -0.6)*/; // paper says positive 0.6, might be a typo, compare to b in Fig 8
      double tc = Math.Pow(b / T0, 0.31) * Math.Pow(10, 1.16);
      return Optimize(initialLocation, F, maxIterations, T0, b, tc, 1, epsilon, nc, npc);
    }

    public static BCOResult Optimize(double[] initialLocation, Func<Vector, double> F, int maxIterations,
      double T0, double b, double tc, double v, double epsilon, int nc, int npc)
    {
      var path = new List<Vector>();
      Vector x = new DenseVector(initialLocation);
      double f = F(x);
      double fChange = 0;
      Vector xChange = new DenseVector(x.Count, T0);
      double tpr = 0;
      Vector bestx = x;
      double bestf = F(x);
      Vector phi = new DenseVector(x.Count - 1, 0);
      Queue<double> slopeCache = new Queue<double>();
      Queue<double> fChangeCache = new Queue<double>();

      for (int i = 0; i < maxIterations; i++)
      {
        // calculate trajectory duration
        double T;
        double lpr = xChange.Norm(2);
        double slope = fChange / lpr;
        slopeCache.Enqueue(slope);
        if (slopeCache.Count > nc)
          slopeCache.Dequeue();
        if (slope >= 0)
          T = T0;
        else
        {
          double bActual;

          if (nc == 0)
            bActual = b;
          else
          {
            double bCorr = b * (1.0 / (1 + Math.Abs(slopeCache.Sum() / nc)));
            if (i == 1) // not 0 because we don't have a non-zero fChange until i == 1
              bCorr *= (1.0 / (1 + Math.Abs(fChange)));
            bActual = bCorr;
          }
          T = T0 * (1.0 + bActual * Math.Abs(fChange / lpr));
        }
        double t = RandomExponential(1.0 / T);

        // calculate new angle(s)
        Vector phiChange = new DenseVector(List.Repeat(phi.Count, _ => CalculateStochasticAngle(tc, tpr, fChange, lpr)).ToArray());
        phi = (Vector)(phi + phiChange);

        // calculate new position
        Vector n = AnglesToUnitVector(phi);
        Vector newx = (Vector)(x + n * v * t);
        double newf = F(newx);

        xChange = (Vector)(newx - x);
        fChange = newf - f;
        fChangeCache.Enqueue(fChange);
        if (fChangeCache.Count > npc)
          fChangeCache.Dequeue();
        tpr = t;

        x = newx;
        f = newf;
        path.Add(x);

        if (f < bestf)
        {
          bestf = f;
          bestx = x;
        }

        if (npc > 0 && fChangeCache.Count == npc && fChangeCache.All(c => Math.Abs(c) < epsilon))
        {
          Trace.WriteLine("Stopped at iteration " + i);
          break;
        }
      }

      return new BCOResult {
        MinimumLocation = bestx.ToArray(),
        MinimumValue = bestf,
        Path = path
      };
    }

    static double Product(Vector v, int start, int end, Func<double, double> f)
    {
      double result = 1;
      for (int i = start; i <= end; i++)
        result *= f(v[i]);
      return result;
    }

    static Vector AnglesToUnitVector(Vector phi)
    {
      var n = phi.Count + 1;
      Vector x = new DenseVector(n);
      x[0] = Product(phi, 0, n - 2, a => Math.Cos(a));
      for (int i = 1; i < n - 1; i++)
        x[i] = Math.Sin(phi[i - 1]) * Product(phi, i, n - 2, a => Math.Cos(a));
      x[n - 1] = Math.Sin(phi[n - 2]);
      return (Vector)x.Normalize(2);
    }

    static double CalculateStochasticAngle(double tc, double tpr, double fChange, double lpr)
    {
      double angleMean;
      double angleStdDev;
      if (fChange / lpr >= 0)
      {
        angleMean = BaseAngleMean;
        angleStdDev = BaseAngleStdDev;
      }
      else
      {
        double cosineApprox = Math.Exp(-tc * tpr);
        angleMean = BaseAngleMean * (1 - cosineApprox);
        angleStdDev = BaseAngleStdDev * (1 - cosineApprox);
      }
      bool turnRight = Random.Next(2) == 0;
      return RandomGaussian(turnRight ? angleMean : -angleMean, angleStdDev);
    }

    static Random Random = new Random();
    public static double RandomExponential(double lambda)
    {
      var u = Random.NextDouble();
      return -1.0 / lambda * Math.Log(1 - u);
    }

    public static double RandomGaussian(double mean, double stdDev)
    {
      var u = Random.NextDouble();
      return Math.Sqrt(2 * Math.Pow(stdDev, 2)) * MathNet.Numerics.SpecialFunctions.ErfInv(2 * u - 1) + mean;
    }

    static double DegreesToRadians(double d)
    {
      return d / 180 * Math.PI;
    }
  }
}
