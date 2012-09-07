using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Double;
using PCW;

namespace Quqe
{
  public class BCOResult
  {
    public double[] MinimumLocation;
    public double MinimumValue;
  }

  public class BCO
  {
    static double BaseAngleMean = DegreesToRadians(62);
    static double BaseAngleStdDev = DegreesToRadians(26);

    public static BCOResult Optimize(double[] initialLocation, Func<Vector, double> F,
      double T0, double b, double tc, double v)
    {
      var numIterations = 1000;
      Vector x = new DenseVector(initialLocation);
      double f = F(x);
      double fChange = 0;
      Vector xChange = new DenseVector(x.Count, 0);
      double tpr = 0;
      Vector bestx = x;
      double bestf = F(x);
      Vector phi = new DenseVector(x.Count - 1, 0);

      List.Repeat(numIterations, i => {
        // calculate trajectory duration
        double T;
        double lpr = xChange.Norm(2);
        if (fChange / lpr >= 0)
          T = T0;
        else
          T = T0 * (1.0 + b * Math.Abs(fChange / lpr));
        double t = RandomExponential(1.0 / T);

        // calculate new angle(s)
        Vector phiChange = new DenseVector(List.Repeat(phi.Count, _ => CalculateStochasticAngle(tc, tpr, fChange, lpr)).ToArray());
        phi = (Vector)(phi + phiChange);

        // calculate new position
        Vector n = AnglesToUnitVector(phi);
        Vector newx = (Vector)(x + n * v * t);
        double newf = F(x);

        xChange = (Vector)(newx - x);
        fChange = newf - f;
        tpr = t;

        x = newx;
        f = newf;

        if (f < bestf)
        {
          bestf = f;
          bestx = x;
        }
      });

      return new BCOResult {
        MinimumLocation = bestx.ToArray(),
        MinimumValue = bestf
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
