using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using System.IO;

namespace Quqe
{
  public enum ActivationType { LogisticSigmoid, Linear }
  public delegate double ActivationFunc(double a);
  public delegate Vector<double> GetRecurrentInputFunc(int layer);

  public class LayerSpec
  {
    public int NodeCount;
    public bool IsRecurrent;
    public ActivationType ActivationType;
  }

  public class RNN
  {
    static RNN() { MathNet.Numerics.Control.DisableParallelization = true; }

    class Layer
    {
      public Matrix<double> W; // [node, input] (input weights)
      public Matrix<double> Wr; // [node, node] (recurrent weights)
      public Vector<double> Bias; // [node] (per-node bias)
      public Vector<double> x; // [input] (input vector)
      public Vector<double> a; // [node] (summed input)
      public Vector<double> z; // [node] (node output)
      public Vector<double> d; // [node] (this node's error contribution, training-only)
      public bool IsRecurrent;
      public ActivationFunc ActivationFunction;
      public ActivationFunc ActivationFunctionPrime;
      public int NodeCount { get { return W.RowCount; } }
      public int InputCount { get { return W.ColumnCount; } }
    }

    class Frame
    {
      public List<Layer> Layers;
    }

    class ErrorInfo
    {
      public double Error;
      public Vector<double> Gradient;
    }

    int NumInputs;
    List<LayerSpec> LayerSpecs;
    List<Layer> Layers;
    const double TimeZeroRecurrentInputValue = 0.5;

    static ErrorInfo EvaluateWeights(RNN net, Vector<double> weights, Matrix trainingData, Vector outputData)
    {
      var time = new List<Frame>();
      var oldWeights = net.GetWeightVector();
      net.SetWeightVector(weights);
      double totalOutputError = 0;

      // propagate inputs forward
      for (int t = 0; t < trainingData.ColumnCount; t++)
      {
        time.Add(new Frame { Layers = SpecsToLayers(net.NumInputs, net.LayerSpecs, true) });
        SetWeightVector(time[t].Layers, weights);

        Propagate(trainingData.Column(t), time[t].Layers, l => t == 0 ? MakeTimeZeroRecurrentInput(net.Layers[l].NodeCount)
          : time[t - 1].Layers[l].z);
      }

      // propagate error backward
      int t_max = trainingData.ColumnCount - 1;
      for (int t = t_max; t >= 0; t--)
      {
        int l_max = time[t].Layers.Count - 1;
        for (int l = l_max; l >= 0; l--)
        {
          var layer = time[t].Layers[l];
          for (int i = 0; i < layer.NodeCount; i++)
          {
            double err;

            // calculate error propagated to next layer
            if (l == l_max)
            {
              err = (outputData[t] - layer.z[i]);
              totalOutputError += 0.5 * Math.Pow(err, 2);
            }
            else
            {
              var subsequentLayer = time[t].Layers[l + 1];
              err = (subsequentLayer.W.Column(i) * subsequentLayer.d);
            }

            // calculate error propagated forward in time (recurrently)
            if (t < t_max && layer.IsRecurrent)
            {
              var nextLayerInTime = time[t + 1].Layers[l];
              err += nextLayerInTime.Wr.Column(i) * nextLayerInTime.d;
            }

            layer.d[i] = err * layer.ActivationFunctionPrime(layer.a[i]);
          }
        }
      }

      // calculate gradient
      var gradientLayers = SpecsToLayers(net.NumInputs, net.LayerSpecs);
      for (int t = 0; t < time.Count; t++)
      {
        for (int l = 0; l < gradientLayers.Count; l++)
        {
          // W
          for (int i = 0; i < gradientLayers[l].NodeCount; i++)
            for (int j = 0; j < gradientLayers[l].InputCount; j++)
              gradientLayers[l].W[i, j] += -1 * time[t].Layers[l].d[i] * time[t].Layers[l].x[j];

          // Wr
          if (t > 0 && gradientLayers[l].IsRecurrent)
            for (int i = 0; i < gradientLayers[l].NodeCount; i++)
              for (int j = 0; j < gradientLayers[l].NodeCount; j++)
                gradientLayers[l].Wr[i, j] += -1 * time[t].Layers[l].d[i] * time[t - 1].Layers[l].z[j];

          // Bias
          for (int i = 0; i < gradientLayers[l].NodeCount; i++)
            gradientLayers[l].Bias[i] += -1 * time[t].Layers[l].d[i]; // (bias input is always 1)
        }
      }

      net.SetWeightVector(oldWeights);

      return new ErrorInfo {
        Error = totalOutputError,
        Gradient = GetWeightVector(gradientLayers)
      };
    }

    public static TrainResult<Vector> TrainBPTT(RNN net, double rate, Matrix trainingData, Vector outputData)
    {
      var outputErrorHistory = new List<double>();

      int epoch_max = 1000;
      int epoch = 0;
      var weights = net.GetWeightVector();
      while (true)
      {
        var errorInfo = EvaluateWeights(net, weights, trainingData, outputData);
        outputErrorHistory.Add(errorInfo.Error);
        weights = weights - rate * errorInfo.Gradient;

        epoch++;

        if (epoch % 100 == 0)
        {
          Trace.WriteLine("Error = " + outputErrorHistory.Last());
          net.ToPng("net" + epoch.ToString("D4") + ".png");
        }

        if (epoch == epoch_max)
          break;
      }

      net.SetWeightVector(weights);

      Trace.WriteLine(epoch + " epochs");

      return new TrainResult<Vector> {
        Params = (Vector)weights,
        CostHistory = outputErrorHistory,
        Cost = outputErrorHistory.Last()
      };
    }

    /// <summary>Scaled Conjugate Gradient algorithm from Williams (1991)</summary>
    public static TrainResult<Vector> TrainSCG(RNN net, int restartPeriod, double tolerance, Matrix trainingData, Vector outputData)
    {
      Func<Vector<double>, Vector<double>, Vector<double>, double, Vector<double>> approximateCurvature =
        (w1, gradientAtW1, searchDirection, sig) => {
          var w2 = w1 + sig * searchDirection;
          var gradientAtW2 = EvaluateWeights(net, w2, trainingData, outputData).Gradient;
          return (gradientAtW2 - gradientAtW1) / sig;
        };

      double lambda_min = double.Epsilon;
      double lambda_max = double.MaxValue;
      double S_max = Math.Min(net.GetWeightVector().Count, restartPeriod);
      double tau = tolerance;

      // 0. initialize variables
      var w = net.GetWeightVector();
      double epsilon = Math.Pow(10, -3);
      double lambda = 1;
      double pi = 0.05;
      var errInfo = EvaluateWeights(net, w, trainingData, outputData);
      var errAtW = errInfo.Error;
      List<double> errHistory = new List<double> { errAtW };
      Vector<double> g = errInfo.Gradient;
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
        double rho = 2 * (EvaluateWeights(net, w + alpha * s, trainingData, outputData).Error - errAtW) / (alpha * mu);
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
          var ei1 = EvaluateWeights(net, w1, trainingData, outputData);
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
        if (S == S_max) // restart
        {
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
        Trace.WriteLine(string.Format("[{0}]  Error = {1}  |g| = {2}", n, errAtW, g.Norm(2)));

        if (n > S_max && g.Norm(2) < tau)
          break;
      }

      net.SetWeightVector(w);

      return new TrainResult<Vector> {
        Params = (Vector)w,
        Cost = errAtW,
        CostHistory = errHistory
      };
    }

    public RNN(int numInputs, List<LayerSpec> layerSpecs)
    {
      NumInputs = numInputs;
      LayerSpecs = layerSpecs;
      Layers = SpecsToLayers(NumInputs, LayerSpecs);
    }

    public void ResetState()
    {
      foreach (var l in Layers)
        l.z = MakeTimeZeroRecurrentInput(l.NodeCount);
    }

    public Vector<double> Propagate(Vector<double> input)
    {
      return Propagate(input, Layers, l => Layers[l].z); // recursive inputs are previous outputs (z)
    }

    static Vector<double> Propagate(Vector<double> input, List<Layer> layers, GetRecurrentInputFunc getRecurrentInput)
    {
      for (int l = 0; l < layers.Count; l++)
      {
        var layer = layers[l];
        PropagateLayer(input, layer, getRecurrentInput(l));
        input = layer.z;
      }
      return layers.Last().z;
    }

    static void PropagateLayer(Vector<double> input, Layer layer, Vector<double> recurrentInput)
    {
      layer.x = input;
      layer.a = layer.W * input + layer.Bias;
      if (layer.IsRecurrent)
        layer.a += layer.Wr * recurrentInput;
      layer.z = ApplyActivationFunction(layer.a, layer.ActivationFunction);
    }

    static Vector<double> ApplyActivationFunction(Vector<double> a, ActivationFunc f)
    {
      Vector<double> z = new DenseVector(a.Count);
      var len = a.Count;
      for (int i = 0; i < len; i++)
        z[i] = f(a[i]);
      return z;
    }

    static List<Layer> SpecsToLayers(int numInputs, List<LayerSpec> specs, bool isTraining = false)
    {
      var layers = new List<Layer>();
      foreach (var s in specs)
      {
        var layer = new Layer {
          W = new DenseMatrix(s.NodeCount, layers.Any() ? layers.Last().z.Count : numInputs),
          Bias = new DenseVector(s.NodeCount),
          IsRecurrent = s.IsRecurrent
        };

        if (s.IsRecurrent)
        {
          layer.Wr = new DenseMatrix(s.NodeCount, s.NodeCount);
          layer.z = MakeTimeZeroRecurrentInput(s.NodeCount);
        }

        if (s.ActivationType == ActivationType.LogisticSigmoid)
        {
          layer.ActivationFunction = LogisticSigmoid;
          layer.ActivationFunctionPrime = LogisticSigmoidPrime;
        }
        else if (s.ActivationType == ActivationType.Linear)
        {
          layer.ActivationFunction = Linear;
          layer.ActivationFunctionPrime = LinearPrime;
        }
        else
          throw new Exception("Unexpected ActivationType: " + s.ActivationType);

        if (isTraining)
          layer.d = new DenseVector(s.NodeCount);

        layers.Add(layer);
      }
      return layers;
    }

    static Vector<double> MakeTimeZeroRecurrentInput(int size)
    {
      return new DenseVector(size, TimeZeroRecurrentInputValue);
    }

    public Vector<double> GetWeightVector()
    {
      return GetWeightVector(Layers);
    }

    static Vector<double> GetWeightVector(List<Layer> layers)
    {
      List<double> weights = new List<double>();
      WalkWeights(layers, w => weights.Add(w), null);
      return new DenseVector(weights.ToArray());
    }

    public void SetWeightVector(Vector<double> weights)
    {
      SetWeightVector(Layers, weights);
      ResetState();
    }

    static void SetWeightVector(List<Layer> layers, Vector<double> weights)
    {
      var i = 0;
      WalkWeights(layers, null, () => {
        var w = weights[i];
        i++;
        return w;
      });
    }

    static void WalkWeights(List<Layer> layers, Action<double> observe, Func<double> getNextValue)
    {
      for (int layer = 0; layer < layers.Count; layer++)
      {
        var l = layers[layer];
        WalkMatrix(l.W, observe, getNextValue);
        if (l.IsRecurrent)
          WalkMatrix(l.Wr, observe, getNextValue);
        WalkVector(layers[layer].Bias, observe, getNextValue);
      }
    }

    static void WalkVector(Vector<double> v, Action<double> observe, Func<double> getNextValue)
    {
      var len = v.Count;
      for (int i = 0; i < len; i++)
        if (getNextValue != null)
          v[i] = getNextValue();
        else
          observe(v[i]);
    }

    static void WalkMatrix(Matrix<double> m, Action<double> observe, Func<double> getNextValue)
    {
      var nRows = m.RowCount;
      var nCols = m.ColumnCount;
      for (int i = 0; i < nRows; i++)
        for (int j = 0; j < nCols; j++)
          if (getNextValue != null)
            m[i, j] = getNextValue();
          else
            observe(m[i, j]);
    }

    static double Linear(double x)
    {
      return x;
    }

    static double LinearPrime(double x)
    {
      return 1;
    }

    static double LogisticSigmoid(double x)
    {
      return 1 / (1 + Math.Exp(-x));
    }

    static double LogisticSigmoidPrime(double x)
    {
      var v = LogisticSigmoid(x);
      return v * (1 - v);
    }

    public static TrainResult<Vector> TrainSA(RNN net, Matrix trainingData, Vector outputData)
    {
      var result = Optimizer.Anneal(net.GetWeightVector().Count, 1, w => {
        net.SetWeightVector(w);
        int correctCount = 0;
        double errorSum = 0;
        for (int i = 0; i < trainingData.ColumnCount; i++)
        {
          var output = net.Propagate(trainingData.Column(i))[0];
          errorSum += Math.Pow(output - outputData[i], 2);
          if (Math.Sign(output) == Math.Sign(outputData[i]))
            correctCount++;
        }
        //return (double)correctCount / trainingData.ColumnCount;
        return errorSum / trainingData.ColumnCount;
      });

      net.SetWeightVector(result.Params);
      return result;
    }

    public void ToPng(string fn)
    {
      var baseName = Path.GetFileNameWithoutExtension(fn);
      ToDot(baseName + ".dot");
      var psi = new ProcessStartInfo(@"C:\Program Files (x86)\Graphviz 2.28\bin\dot.exe",
        string.Format("-Tpng -o dots\\{0}.png {0}.dot", baseName));
      psi.CreateNoWindow = true;
      psi.WindowStyle = ProcessWindowStyle.Hidden;
      var p = Process.Start(psi);
      p.EnableRaisingEvents = true;
      p.WaitForExit();
    }

    public void ToDot(string fn)
    {
      using (var op = new StreamWriter(fn))
      {
        op.WriteLine(@"
digraph {
rankdir=LR;
nodesep=0.5;
ranksep=5;
ordering=out;
edge [ arrowsize=0.5 ];
node [ shape=circle ];");

        op.WriteLine("rootnode [ style=invis ];");

        var inputs = new List<string>();
        op.WriteLine(@"
subgraph {
rank=same;");
        for (int i = 0; i < NumInputs; i++)
        {
          var name = "input" + i;
          inputs.Add(name);
          op.WriteLine("input{0} [ shape=square, label=\"{0}\" ];", i);
        }
        op.WriteLine("}");
        for (int i = 0; i < NumInputs; i++)
          op.WriteLine("rootnode -> {0} [ style=invis ];", inputs[i]);

        for (int l = 0; l < Layers.Count; l++)
        {
          var newInputs = new List<string>();
          op.WriteLine("subgraph {\r\nrank=same;\r\n");
          for (int i = 0; i < Layers[l].NodeCount; i++)
          {
            var name = "L" + l + "_" + i;
            newInputs.Add(name);
            op.WriteLine(name + ";");
          }
          op.WriteLine("}");

          for (int i = 0; i < Layers[l].NodeCount; i++)
            for (int j = 0; j < inputs.Count; j++)
              op.WriteLine("{0} -> {1} [ color={2}, penwidth={3:N2} ];",
                inputs[j], newInputs[i], Layers[l].W[i, j] > 0 ? "black" : "red", Math.Abs(5 * Layers[l].W[i, j]));

          if (Layers[l].IsRecurrent)
            for (int i = 0; i < Layers[l].NodeCount; i++)
              for (int j = 0; j < Layers[l].NodeCount; j++)
                op.WriteLine("{0} -> {1} [ constraint=false, color={2}, penwidth={3} ];",
                  newInputs[j], newInputs[i], Layers[l].Wr[i, j] > 0 ? "black" : "red", Math.Abs(5 * Layers[l].Wr[i, j]));

          inputs = newInputs;
        }

        op.WriteLine("}");
      }
    }
  }
}
