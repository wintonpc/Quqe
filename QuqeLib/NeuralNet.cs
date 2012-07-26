using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.Diagnostics;
using System.IO;

namespace Quqe
{
  public class Example
  {
    public double[] Inputs { get; set; }
    public double[] BestOutputs { get; set; }
  }

  public class Example<T> : Example
  {
    public T Tag { get; set; }
  }

  public class WardNet : NeuralNet
  {
    static readonly List<Func<double, double>> ActivationFunction = List.Create<Func<double, double>>(Math.Tanh, Gaussian);

    public WardNet(int numInputs, Genome g)
      : this(numInputs)
    {
      FromGenome(g);
    }

    public WardNet(int numInputs)
      : base(List.Repeat(numInputs, () => ""), List.Create(2), List.Create("Output"))
    {
    }

    void FromGenome(Genome g)
    {
      var genes = new Queue<double>(g.Genes);
      for (int layer = 0; layer < Weights.Count; layer++)
      {
        var w = Weights[layer];
        for (int input = 0; input < w.RowCount(); input++)
          for (int node = 0; node < w.ColCount(); node++)
            w[input, node] = genes.Dequeue();
      }
      if (genes.Any()) // it's an older genome with activation function genes
      {
        for (int layer = 0; layer < Weights.Count - 1 /* -1 to leave output layer alone */; layer++)
          for (int node = 0; node < Weights[layer].ColCount(); node++)
            ActivationFunctions[layer, node] = ActivationFunctionFromGene(genes.Dequeue());
      }
      else
      {
        ActivationFunctions[0, 0] = Math.Tanh;
        ActivationFunctions[0, 1] = Gaussian;
      }
    }

    public new Genome ToGenome()
    {
      var g = new List<double>();
      for (int layer = 0; layer < Weights.Count; layer++)
      {
        var w = Weights[layer];
        for (int input = 0; input < w.RowCount(); input++)
          for (int node = 0; node < w.ColCount(); node++)
            g.Add(w[input, node]);
      }
      //for (int layer = 0; layer < Weights.Count - 1 /* -1 to leave output layer alone */; layer++)
      //  for (int node = 0; node < Weights[layer].ColCount(); node++)
      //    g.Add(ActivationFunctionToGene(ActivationFunctions[layer, node]));
      return new Genome { Genes = g };
    }

    //static double ActivationFunctionToGene(Func<double, double> f)
    //{
    //  return f == Math.Tanh ? 0.5 : -0.5;
    //}

    static Func<double, double> ActivationFunctionFromGene(double gene)
    {
      return gene >= 0 ? (Func<double, double>)Math.Tanh : (Func<double, double>)Gaussian;
    }

    public static int GenomeSize(int numInputs)
    {
      return new WardNet(numInputs).ToGenome().Size;
    }
  }

  public class NeuralNet
  {
    List<string> InputNames;
    List<string> OutputNames;
    protected List<double[,]> Weights; // Weights[layer][input, node]
    protected Func<double, double>[,] ActivationFunctions; // ActivationFunctions[layer, node]

    public NeuralNet(int numInputs, int numHidden)
      : this(List.Repeat(numInputs, () => ""), List.Create(numHidden), List.Create(""))
    {
    }

    public NeuralNet(int numInputs, int numHidden, Genome g)
      : this(List.Repeat(numInputs, () => ""), List.Create(numHidden), List.Create(""))
    {
      FromGenome(g);
    }

    void FromGenome(Genome g)
    {
      var genes = new Queue<double>(g.Genes);
      for (int layer = 0; layer < Weights.Count; layer++)
      {
        var w = Weights[layer];
        for (int input = 0; input < w.RowCount(); input++)
          for (int node = 0; node < w.ColCount(); node++)
            w[input, node] = genes.Dequeue();
      }
    }

    public Genome ToGenome()
    {
      var g = new List<double>();
      for (int layer = 0; layer < Weights.Count; layer++)
      {
        var w = Weights[layer];
        for (int input = 0; input < w.RowCount(); input++)
          for (int node = 0; node < w.ColCount(); node++)
            g.Add(w[input, node]);
      }
      return new Genome { Genes = g };
    }

    public NeuralNet(IEnumerable<string> inputs, IEnumerable<int> hiddenLayers, IEnumerable<string> outputs)
    {
      InputNames = inputs.ToList();
      OutputNames = outputs.ToList();
      Weights = MakeHiddenLayers(InputNames.Count, hiddenLayers);
      Weights.Add(new double[Weights.Any() ? Weights.Last().NodeCount() : InputNames.Count, OutputNames.Count]); // output weights
      ActivationFunctions = new Func<double, double>[Weights.Count, hiddenLayers.Max()];
      for (int layer = 0; layer < ActivationFunctions.RowCount(); layer++)
        for (int i = 0; i < ActivationFunctions.ColCount(); i++)
          ActivationFunctions[layer, i] = Identity;
    }

    public NeuralNet(IEnumerable<string> inputs, IEnumerable<string> outputs, IEnumerable<double[,]> weights)
    {
      InputNames = inputs.ToList();
      OutputNames = outputs.ToList();
      Weights = weights.ToList();
    }

    public double[] Propagate(IEnumerable<double> inputs)
    {
      return Propagate(inputs.ToArray(), 0);
    }

    double[] Propagate(double[] inputs, int layerNum)
    {
      return Propagate(inputs, layerNum, null, null);
    }

    double[] Propagate(double[] inputs, int layerNum, double[,] inValues, double[,] outValues)
    {
      if (layerNum >= Weights.Count)
        return inputs;
      var w = Weights[layerNum];
      return Propagate(Repeat(w.NodeCount(), nodeNum => {
        var inValue = DotProduct(inputs, w.Col(nodeNum));
        var outValue = ActivationFunctions[layerNum, nodeNum](inValue);
        if (inValues != null)
          inValues[layerNum, nodeNum] = inValue;
        if (outValues != null)
          outValues[layerNum, nodeNum] = outValue;
        return outValue;
      }), layerNum + 1, inValues, outValues);
    }

    //public void GradientlyDescend(double learningRate, double threshold, List<Example> examples, Action afterEpoch)
    //{
    //  bool anyExceeded;
    //  do
    //  {
    //    anyExceeded = false;
    //    foreach (var ex in examples)
    //    {
    //      double[,] inValues = new double[Weights.Count, Weights.Max(w => w.NodeCount())];
    //      double[,] outValues = new double[Weights.Count, Weights.Max(w => w.NodeCount())];
    //      double[,] deltas = new double[Weights.Count, Weights.Max(w => w.NodeCount())];
    //      var outputs = Propagate(ex.Inputs, 0, inValues, outValues);

    //      int outputLayerNum = Weights.Count - 1;
    //      var outputW = Weights[outputLayerNum];
    //      for (int nodeNum = 0; nodeNum < outputW.NodeCount(); nodeNum++)
    //        deltas[outputLayerNum, nodeNum] = ActivationFunctionPrime(inValues[outputLayerNum, nodeNum]) * (ex.BestOutputs[nodeNum] - outputs[nodeNum]);

    //      for (int layerNum = Weights.Count - 2; layerNum >= 0; layerNum--)
    //      {
    //        var w = Weights[layerNum];
    //        for (int j = 0; j < w.NodeCount(); j++)
    //        {
    //          var weightsFromThisNode = Weights[layerNum + 1].Row(j).ToArray();
    //          deltas[layerNum, j] = ActivationFunctionPrime(inValues[layerNum, j]) * DotProduct(weightsFromThisNode, deltas.Row(layerNum + 1).Take(weightsFromThisNode.Length));

    //          var forwardW = Weights[layerNum + 1];
    //          for (int i = 0; i < forwardW.NodeCount(); i++)
    //            forwardW[j, i] += learningRate * outValues[layerNum, j] * deltas[layerNum + 1, i];
    //        }
    //      }

    //      var firstW = Weights[0];
    //      for (int j = 0; j < ex.Inputs.Length; j++)
    //        for (int i = 0; i < firstW.NodeCount(); i++)
    //          firstW[j, i] += learningRate * ex.Inputs[j] * deltas[0, i];
    //    }
    //    afterEpoch();
    //  } while (true);
    //}

    //double[] Propagate(double[] inputs, int layerNum)
    //{
    //  if (layerNum >= Weights.Count)
    //    return inputs;
    //  var w = Weights[layerNum];
    //  return Propagate(Repeat(w.NodeCount(), nodeNum => ActivationFunction(DotProduct(AddBiasInput(inputs), w.Col(nodeNum)))), layerNum + 1);
    //}

    public void JiggleWeights()
    {
      foreach (var w in Weights)
        for (int i = 0; i < w.RowCount(); i++)
          for (int j = 0; j < w.ColCount(); j++)
            w[i, j] += Random.NextDouble() * 10;
    }

    public void RandomizeWeights()
    {
      Weights = Weights.Select(w => MakeRandomWeightTable(w.RowCount(), w.ColCount())).ToList();
    }

    public void SetWeights(double weightValue)
    {
      Weights = Weights.Select(w => MakeWeightTable(w.RowCount(), w.ColCount(), (r, c) => weightValue)).ToList();
    }

    static Random Random = new Random();
    static double[,] MakeRandomWeightTable(int nRows, int nCols)
    {
      return MakeWeightTable(nRows, nCols, (r, c) => Random.NextDouble() * 20 - 10);
    }

    static double[,] MakeWeightTable(int nRows, int nCols, Func<int, int, double> makeValue)
    {
      var result = new double[nRows, nCols];
      for (int i = 0; i < nRows; i++)
        for (int j = 0; j < nCols; j++)
          result[i, j] = makeValue(i, j);
      return result;
    }

    static double[] Repeat(int n, Func<int, double> f)
    {
      var result = new double[n];
      for (int i = 0; i < n; i++)
        result[i] = f(i);
      return result;
    }

    public static double DotProduct(IEnumerable<double> a, IEnumerable<double> b)
    {
      double result = 0;
      var ae = a.GetEnumerator();
      var be = b.GetEnumerator();
      while (ae.MoveNext())
      {
        if (!be.MoveNext())
          throw new ArgumentException("Argument lengths must be equal.");
        result += ae.Current * be.Current;
      }
      if (be.MoveNext())
        throw new ArgumentException("Argument lengths must be equal.");
      return result;
    }

    static List<double[,]> MakeHiddenLayers(int nInputs, IEnumerable<int> hiddenLayers)
    {
      return hiddenLayers.Select(nodeCount => {
        var thisInputCount = nInputs;
        nInputs = nodeCount; // mutation!
        return new double[thisInputCount, nodeCount];
      }).ToList();
    }

    protected static double Identity(double x)
    {
      return x;
    }

    protected static double Gaussian(double x)
    {
      return Math.Exp(-x * x / 2);
    }

    static double Sigmoid(double x)
    {
      return 1 / (1 + Math.Exp(-x));
    }

    static double SigmoidFirstDerivative(double x)
    {
      return Sigmoid(x) * (1 - Sigmoid(x));
    }

    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine(InputNames.Join(" "));
      sb.AppendLine(OutputNames.Join(" "));
      foreach (var w in Weights)
      {
        sb.AppendFormat("{0} {1}\r\n", w.RowCount(), w.ColCount());
        foreach (var r in w.Rows())
          sb.AppendLine(r.Join("\t"));
      }
      return sb.ToString();
    }

    public static NeuralNet FromString(string s)
    {
      using (var ip = new StringReader(s))
      {
        var inputNames = ip.ReadLine().Trim().Split(' ');
        var outputNames = ip.ReadLine().Trim().Split(' ');
        var weights = new List<double[,]>();
        string line;
        while ((line = ip.ReadLine()) != null)
        {
          var dimensions = line.Trim().Split(' ').Select(d => int.Parse(d)).ToArray();
          var w = new double[dimensions[0], dimensions[1]];
          for (int i = 0; i < dimensions[0]; i++)
          {
            var row = ip.ReadLine().Trim().Split('\t').Select(z => double.Parse(z)).ToArray();
            for (int j = 0; j < row.Length; j++)
              w[i, j] = row[j];
          }
          weights.Add(w);
        }
        return new NeuralNet(inputNames, outputNames, weights);
      }
    }
  }

  public static class NNUtil
  {
    public static int RowCount<T>(this T[,] array)
    {
      return array.GetLength(0);
    }

    public static int InputCount<T>(this T[,] array)
    {
      return array.GetLength(0);
    }

    public static int ColCount<T>(this T[,] array)
    {
      return array.GetLength(1);
    }

    public static int NodeCount<T>(this T[,] array)
    {
      return array.GetLength(1);
    }

    public static IEnumerable<T> Row<T>(this T[,] array, int rowNum)
    {
      for (int c = 0; c < array.ColCount(); c++)
        yield return array[rowNum, c];
    }

    public static IEnumerable<T> Col<T>(this T[,] array, int colNum)
    {
      for (int r = 0; r < array.RowCount(); r++)
        yield return array[r, colNum];
    }

    public static IEnumerable<IEnumerable<T>> Rows<T>(this T[,] array)
    {
      for (int r = 0; r < array.RowCount(); r++)
        yield return array.Row(r);
    }
  }
}
