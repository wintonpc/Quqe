using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using PCW;
using System.Xml.Linq;
using System.Threading.Tasks;

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

  public class RNN : IPredictor
  {
    static RNN() { MathNet.Numerics.Control.DisableParallelization = true; }
    public static bool ShouldTrace = true;

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

    class WeightEvalInfo
    {
      public Vector<double> Output;
      public double Error;
      public Vector<double> Gradient;
    }

    int NumInputs;
    List<LayerSpec> LayerSpecs;
    List<Layer> Layers;
    const double TimeZeroRecurrentInputValue = 0.5;

    /// <summary>Scaled Conjugate Gradient algorithm from Williams (1991)</summary>
    public static RnnTrainResult TrainSCGInternal(List<LayerSpec> layerSpecs, Vector<double> weights, double epoch_max, Matrix<double> trainingData, Vector<double> outputData)
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

    /// <summary>Scaled Conjugate Gradient algorithm from Williams (1991)</summary>
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

    const int ACTIVATION_LOGSIG = 0;
    const int ACTIVATION_PURELIN = 1;
    struct QMLayerSpec
    {
      public QMLayerSpec(LayerSpec spec)
      {
        NodeCount = spec.NodeCount;
        IsRecurrent = spec.IsRecurrent;
        ActivationType = spec.ActivationType == Quqe.ActivationType.LogisticSigmoid ?
          ACTIVATION_LOGSIG : ACTIVATION_PURELIN;
      }

      int NodeCount;
      bool IsRecurrent;
      int ActivationType;
    };

    static WeightEvalInfo EvaluateWeightsFast(IntPtr context, double[] weights, int numOutputs)
    {
      double error;
      double[] grad = new double[weights.Length];
      double[] output = new double[numOutputs];
      QMEvaluateWeights(context, weights, weights.Length, output, out error, grad);
      return new WeightEvalInfo {
        Output = new DenseVector(output),
        Error = error,
        Gradient = new DenseVector(grad)
      };
    }

    [DllImport("QuqeMath.dll", EntryPoint = "CreateWeightContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreateWeightContext(QMLayerSpec[] layerSpecs, int numLayers, double[] trainingData, double[] outputData,
      int nInputs, int nSamples);

    [DllImport("QuqeMath.dll", EntryPoint = "EvaluateWeights", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMEvaluateWeights(IntPtr context, double[] weights, int nWeights, double[] output, out double error, double[] gradient);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroyWeightContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMDestroyWeightContext(IntPtr context);

    [DllImport("QuqeMath.dll", EntryPoint = "CreatePropagationContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMCreatePropagationContext(QMLayerSpec[] layerSpecs, int numLayers, int nInputs, double[] weights, double nWeights);

    [DllImport("QuqeMath.dll", EntryPoint = "PropagateInput", CallingConvention = CallingConvention.Cdecl)]
    extern static void QMPropagateInput(IntPtr context, double[] input, double[] output);

    [DllImport("QuqeMath.dll", EntryPoint = "DestroPropagationContext", CallingConvention = CallingConvention.Cdecl)]
    extern static IntPtr QMDestroyPropagationContext(IntPtr context);

    public RNN(int numInputs, List<LayerSpec> layerSpecs)
    {
      NumInputs = numInputs;
      LayerSpecs = layerSpecs;
      Layers = SpecsToLayers(NumInputs, LayerSpecs);
      SetWeightVector(Optimizer.RandomVector(GetWeightVector().Count, -1, 1));
    }

    public void ResetState()
    {
      foreach (var l in Layers)
        l.z = MakeTimeZeroRecurrentInput(l.NodeCount);
    }

    public Vector<double> Propagate(Vector<double> input)
    {


      IntPtr context = QMCreateWeightContext(LayerSpecs.Select(spec => new QMLayerSpec(spec)).ToArray(),
        LayerSpecs.Count, trainingData.ToRowWiseArray(), outputData.ToArray(),
        trainingData.RowCount, trainingData.ColumnCount);

      var wei = EvaluateWeightsFast(context, w.ToArray(), 1);
      
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
        WalkVector(l.Bias, observe, getNextValue);
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

    public double Predict(Vector<double> input)
    {
      return Propagate(input)[0];
    }

    public void Reset()
    {
      ResetState();
    }
  }
}
