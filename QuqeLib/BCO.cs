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
    public double[] Minimum;
    public double Cost;
  }

  public class BCO
  {
    static double BaseAngleMean = DegreesToRadians(62);
    static double BaseAngleStdDev = DegreesToRadians(26);

    public static BCOResult Optimize(double[] initialLocation, Func<Vector, double> f,
      double T0, double b, double tc, double v)
    {
      var numIterations = 1000;
      Vector x = new DenseVector(initialLocation);
      Vector lastx = x;
      double lastf = f(x);
      Vector best = x;
      double bestf = lastf;
      double tpr = T0;
      double[] phi = new double[x.Count - 1];
      List.Repeat(numIterations, i => {
        double T;
        double thisf = f(x);
        if (thisf < bestf)
        {
          bestf = thisf;
          best = x;
        }
        double fpr = thisf - lastf;

        // calculate trajectory duration
        double lpr = (x - lastx).Norm(2);
        if (fpr / lpr >= 0)
          T = T0;
        else
          T = T0 * (1.0 + b * Math.Abs(fpr / lpr));
        double t = RandomExponential(1.0 / T);

        // calculate angle
        CalculateStochasticAngle(tc, tpr, fpr, lpr);

        // calculate new position
        Vector n = null; // TODO
        x = (Vector)(x + n.Normalize(2) * v * t);

        lastx = x;
        lastf = thisf;
        tpr = t;
      });
      return null;
    }

    private static double CalculateStochasticAngle(double tc, double tpr, double fpr, double lpr)
    {
      double angleMean;
      double angleStdDev;
      if (fpr / lpr >= 0)
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
