using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra.Generic;

namespace Quqe
{
  public partial class RNN
  {
    static RNN() { MathNet.Numerics.Control.DisableParallelization = true; }
    public static bool ShouldTrace = true;

    public static RnnTrainResult TrainSCG(RNN net, double epoch_max, Matrix<double> trainingData, Vector<double> outputData, Vector<double> initialWeights)
    {
      var scgInit = (initialWeights != null && initialWeights.Count == net.GetWeightVector().Count) ? initialWeights : net.GetWeightVector();
      var result = TrainSCGInternal(net.LayerSpecs, scgInit, epoch_max, trainingData, outputData);
      net.SetWeightVector(result.Params);
      return result;
    }

    public static RnnTrainResult TrainSCGMulti(RNN net, double epoch_max, Matrix<double> trainingData, Vector<double> outputData, int numTrials, Vector<double> initialWeights)
    {
      object theLock = new object();
      var results = new List<RnnTrainResult>();
      Action trainOne = () => {
        var result = TrainSCGInternal(net.LayerSpecs, Optimizer.RandomVector(net.GetWeightVector().Count, -1, 1), epoch_max, trainingData, outputData);
        lock (theLock) { results.Add(result); }
      };
      //Parallel.For(0, numTrials, n => trainOne());
      for (int i = 0; i < numTrials; i++)
        trainOne();
      return results.OrderBy(r => r.Cost).First();
    }

    /// <summary>Scaled Conjugate Gradient algorithm from Williams (1991)</summary>
    static RnnTrainResult TrainSCGInternal(List<LayerSpec> layerSpecs, Vector<double> weights, double epoch_max, Matrix<double> trainingData, Vector<double> outputData)
    {
      var initialWeights = weights;
      Stopwatch sw = new Stopwatch();
      sw.Start();
      IntPtr context = QMCreateWeightContext(layerSpecs.Select(spec => new QMLayerSpec(spec)).ToArray(),
        layerSpecs.Count, trainingData.ToRowWiseArray(), outputData.ToArray(),
        trainingData.RowCount, trainingData.ColumnCount);

      Func<Vector<double>, Vector<double>, Vector<double>, double, Vector<double>> approximateCurvature =
        (w1, gradientAtW1, searchDirection, sig) => {
          var w2 = w1 + sig * searchDirection;
          //var gradientAtW2 = EvaluateWeights(net, w2, trainingData, outputData).Gradient;
          var gradientAtW2 = EvaluateWeightsFast(context, w2.ToArray(), 1).Gradient;
          return (gradientAtW2 - gradientAtW1) / sig;
        };

      double lambda_min = double.Epsilon;
      double lambda_max = double.MaxValue;
      double S_max = weights.Count;
      double tau = 0.00001;

      // 0. initialize variables
      var w = weights;
      double epsilon = Math.Pow(10, -3);
      double lambda = 1;
      double pi = 0.05;

      var wei = EvaluateWeightsFast(context, w.ToArray(), 1);

      var errAtW = wei.Error;
      List<double> errHistory = new List<double> { errAtW };
      Vector<double> g = wei.Gradient;
      Vector<double> s = -g;
      bool success = true;
      int S = 0;

      double kappa = 0; // will be assigned in (1) on first iteration
      double sigma = 0; // will be assigned in (1) on first iteration
      double gamma = 0; // will be assigned in (1) on first iteration
      double mu = 0;    // will be assigned in (1) on first iteration
      int n = 0;
      while (true)
      {
        // 1. if success == true, calculate first and second order directional derivatives
        if (success)
        {
          mu = s.DotProduct(g); // (directional gradient)
          if (mu >= 0)
          {
            s = -g;
            mu = s.DotProduct(g);
            S = 0;
          }
          kappa = s.DotProduct(s);
          sigma = epsilon / Math.Sqrt(kappa);
          gamma = s.DotProduct(approximateCurvature(w, g, s, sigma)); // (directional curvature)
        }

        // 2. increase the working curvature
        double delta = gamma + lambda * kappa;

        // 3. if delta <= 0, make delta positive and increase lambda
        if (delta <= 0)
        {
          delta = lambda * kappa;
          lambda = lambda - gamma / kappa;
        }

        // 4. calculate step size and adapt epsilon
        double alpha = -mu / delta;
        double epsilon1 = epsilon * Math.Pow(alpha / sigma, pi);

        // 5. calculate the comparison ratio
        double rho = 2 * (EvaluateWeightsFast(context, (w + alpha * s).ToArray(), 1).Error - errAtW) / (alpha * mu);
        success = rho >= 0;

        // 6. revise lambda
        double lambda1;
        if (rho < 0.25)
          lambda1 = Math.Min(lambda + delta * (1 - rho) / kappa, lambda_max);
        else if (rho > 0.75)
          lambda1 = Math.Max(lambda / 2, lambda_min);
        else
          lambda1 = lambda;

        // 7. if success == true, adjust weights
        Vector<double> w1;
        double errAtW1;
        Vector<double> g1;
        if (success)
        {
          w1 = w + alpha * s;
          var ei1 = EvaluateWeightsFast(context, w1.ToArray(), 1);
          errAtW1 = ei1.Error;
          g1 = ei1.Gradient;
          S++;
        }
        else
        {
          errAtW1 = errAtW;
          w1 = w;
          g1 = g;
        }

        // 8. choose the new search direction
        Vector<double> s1;
        if (S == S_max || (S >= 2 && g.DotProduct(g1) >= 0.2 * g1.DotProduct(g1))) // Powell-Beale restarts
        {
          //Trace.WriteLine("*** RESTARTED ***");
          s1 = -g1;
          success = true;
          S = 0;
        }
        else
        {
          if (success) // create new conjugate direction
          {
            double beta = (g - g1).DotProduct(g1) / mu;
            s1 = -g1 + beta * s;
          }
          else // use current direction again
          {
            s1 = s;
            // mu, kappa, sigma, and gamma stay the same;
          }
        }

        // 9. check tolerance and keep iterating if we're not there yet
        epsilon = epsilon1;
        lambda = lambda1;
        errAtW = errAtW1;
        errHistory.Add(errAtW);
        g = g1;
        s = s1;
        w = w1;

        n++;
        bool done = n == epoch_max || n > 10 && g.Norm(2) < tau;
        if (ShouldTrace && (n == 1 || n % 50 == 0 || done))
          Trace.WriteLine(string.Format("[{0}]  Error = {1}  |g| = {2}", n, errAtW, g.Norm(2)));

        if (done) break;
      }

      QMDestroyWeightContext(context);
      sw.Stop();
      if (ShouldTrace)
        Trace.WriteLine(string.Format("Finished in {0:N2}s", (double)sw.ElapsedMilliseconds / 1000));

      return new RnnTrainResult {
        Params = w,
        Cost = errAtW,
        CostHistory = errHistory,
        TrainingInit = initialWeights
      };
    }
  }
}
