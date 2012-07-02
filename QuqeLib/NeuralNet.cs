using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.Diagnostics;
using System.IO;

namespace Quqe
{
  public class NeuralNet
  {
    List<string> InputNames;
    List<string> OutputNames;
    List<double[,]> Weights; // Weights[layer][input, node]

    public NeuralNet(IEnumerable<string> inputs, IEnumerable<int> hiddenLayers, IEnumerable<string> outputs)
    {
      InputNames = inputs.ToList();
      OutputNames = outputs.ToList();
      Weights = MakeHiddenLayers(InputNames.Count, hiddenLayers);
      Weights.Add(new double[Weights.Last().NodeCount() + 1 /* +1 for bias weight */, OutputNames.Count]); // output weights
      RandomizeWeights();
    }

    public NeuralNet(IEnumerable<string> inputs, IEnumerable<string> outputs, IEnumerable<double[,]> weights)
    {
      InputNames = inputs.ToList();
      OutputNames = outputs.ToList();
      Weights = weights.ToList();
    }

    public double[] Propagate(double[] inputs)
    {
      return Propagate(inputs, 0);
    }

    public void Anneal(int iterations, Func<int, double> schedule, Func<NeuralNet, double> computeCost)
    {
      //var acceptCount = 0;
      //var rejectCount = 0;
      for (int i = 0; i < iterations; i++)
      {
        var temperature = schedule(i);
        var currentCost = computeCost(this);
        var current = Weights.ToList();
        RandomizeWeights();
        var nextCost = computeCost(this);

        if (i % 100 == 0)
        {
          //Console.WriteLine(string.Format("{0} / {1}  Temp = {2:N2}  Accept rate = {3}  ( {4} / {5} )  Cost = {6:N4}",
          //  i, iterations, temperature, rejectCount == 0 ? "always" : ((double)acceptCount / (double)rejectCount).ToString("N3"),
          //  acceptCount, rejectCount, currentCost));
          Console.WriteLine(string.Format("{0} / {1}  Cost = {2:N4}",
            i, iterations, currentCost));
          //acceptCount = 0;
          //rejectCount = 0;
        }

        if (nextCost < currentCost)
          continue;
        else
          Weights = current;
        //else
        //{
        //  bool takeItAnyway = Random.NextDouble() < Math.Exp((currentCost - nextCost) / temperature);
        //  if (takeItAnyway)
        //  {
        //    acceptCount++;
        //    continue;
        //  }
        //  else
        //  {
        //    rejectCount++;
        //    Weights = current;
        //  }
        //}
      }
    }

    public void GradientlyDescend(double threshold)
    {

    }

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

    static Random Random = new Random();
    static double[,] MakeRandomWeightTable(int nRows, int nCols)
    {
      var result = new double[nRows, nCols];
      for (int i = 0; i < nRows; i++)
        for (int j = 0; j < nCols; j++)
          result[i, j] = Random.NextDouble() * 20 - 10;
      return result;
    }

    double[] Propagate(double[] inputs, int layerNum)
    {
      if (layerNum >= Weights.Count)
        return inputs;
      var w = Weights[layerNum];
      return Propagate(Repeat(w.NodeCount(), nodeNum => ActivationFunction(DotProduct(AddBiasInput(inputs), w.Col(nodeNum)))), layerNum + 1);
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

    static double[] AddBiasInput(double[] inputs)
    {
      var newInputs = new double[inputs.Length + 1];
      newInputs[0] = -1;
      Array.Copy(inputs, 0, newInputs, 1, inputs.Length);
      return newInputs;
    }

    static List<double[,]> MakeHiddenLayers(int nInputs, IEnumerable<int> hiddenLayers)
    {
      return hiddenLayers.Select(nodeCount => {
        var thisInputCount = nInputs + 1; // +1 for bias weight
        nInputs = nodeCount; // mutation!
        return new double[thisInputCount, nodeCount];
      }).ToList();
    }

    static double ActivationFunction(double x)
    {
      return 1 / (1 + Math.Exp(-x)); // sigmoid
    }

    static double ActivationFunctionPrime(double x)
    {
      return ActivationFunction(x) * (1 - ActivationFunction(x)); // sigmoid first derivative
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
