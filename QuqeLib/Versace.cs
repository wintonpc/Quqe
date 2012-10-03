using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using YamlDotNet.RepresentationModel.Serialization;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Xml;

namespace Quqe
{
  public static class Versace
  {
    public const int EXPERTS_PER_MIXTURE = 10;
    public const int POPULATION_SIZE = 10;
    public const int SELECTION_SIZE = 4;
    public const int EPOCH_COUNT = 100;
    public const double MUTATION_RATE = 0.05;

    public static List<string> Tickers = List.Create(
      "DIA", "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP", "^GDAX", "^FTSE", /*"^CJJ", "USDCHF"*/
      "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU"
      );

    static DateTime StartDate = DateTime.Parse("11/11/2001");
    static DateTime EndDate = DateTime.Parse("02/12/2003");

    static Versace()
    {
      LoadNormalizedValues();
    }

    public static List<DataSeries<Bar>> GetCleanSeries()
    {
      var raw = Tickers.Select(t => Data.LoadVersace(t)).ToList();
      var dia = raw.First(s => s.Symbol == "DIA");
      var supp = raw.Where(s => s != dia).ToList();

      // fill in missing data in supplemental instruments
      var supp2 = supp.Select(s => {
        var q = (from d in dia
                 join x in s on d.Timestamp equals x.Timestamp into joined
                 from j in joined.DefaultIfEmpty()
                 select new { Timestamp = d.Timestamp, X = j }).ToList();
        List<Bar> newElements = new List<Bar>();
        for (int i = 0; i < q.Count; i++)
        {
          if (q[i].X != null)
            newElements.Add(q[i].X);
          else
          {
            var prev = newElements.Last();
            newElements.Add(new Bar(q[i].Timestamp, prev.Open, prev.Low, prev.High, prev.Close, prev.Volume));
          }
        }
        return new DataSeries<Bar>(s.Symbol, newElements);
      }).ToList();

      return supp2.Concat(List.Create(dia)).ToList();
    }

    public static Matrix TrainingInput { get; private set; }
    public static Matrix ValidationInput { get; private set; }
    public static Vector TrainingOutput { get; private set; }
    public static Vector ValidationOutput { get; private set; }
    public static int DatabaseADimension { get; private set; }
    public static DataSeries<Bar> DIA { get; private set; }

    private static void LoadNormalizedValues()
    {
      var clean = GetCleanSeries();
      var aOnly = new List<DataSeries<Value>>();
      var bOnly = new List<DataSeries<Value>>();

      Func<string, DataSeries<Bar>> get = ticker => clean.First(x => x.Symbol == ticker);

      Action<string, Func<Bar, Value>> addSmaNorm = (ticker, getValue) =>
        aOnly.Add(get(ticker).NormalizeSma10(getValue));

      Action<string> addSmaNormOHLC = (ticker) => {
        addSmaNorm(ticker, x => x.Open);
        addSmaNorm(ticker, x => x.High);
        addSmaNorm(ticker, x => x.Low);
        addSmaNorm(ticker, x => x.Close);
      };

      DIA = get("DIA");

      // % ROC Close
      bOnly.Add(get("DIA").MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return (s[0].Close - s[1].Close) / s[1].Close * 100;
      }));

      // % Diff Open-Close
      bOnly.Add(get("DIA").MapElements<Value>((s, v) => (s[0].Open - s[0].Close) / s[0].Open * 100));

      // % Diff High-Low
      bOnly.Add(get("DIA").MapElements<Value>((s, v) => (s[0].High - s[0].Low) / s[0].Low * 100));

      // my own LinReg stuff
      {
        var fast = get("DIA").Closes().LinReg(2, 1);
        var slow = get("DIA").Closes().LinReg(7, 1);
        bOnly.Add(fast.ZipElements<Value, Value>(slow, (f, s, _) => f[0] - s[0]));
        bOnly.Add(get("DIA").Closes().RSquared(10));
        bOnly.Add(get("DIA").Closes().LinRegSlope(4));
      }

      addSmaNormOHLC("DIA");
      addSmaNorm("DIA", x => x.Volume);

      bOnly.Add(get("DIA").ChaikinVolatility(10));
      bOnly.Add(get("DIA").Closes().MACD(10, 21));
      bOnly.Add(get("DIA").Closes().Momentum(10));
      bOnly.Add(get("DIA").VersaceRSI(10));

      addSmaNormOHLC("^IXIC");
      addSmaNormOHLC("^GSPC");
      addSmaNormOHLC("^DJI");
      addSmaNormOHLC("^DJT");
      addSmaNormOHLC("^DJU");
      addSmaNormOHLC("^DJA");
      addSmaNormOHLC("^N225");
      addSmaNormOHLC("^BVSP");
      addSmaNormOHLC("^GDAX");
      addSmaNormOHLC("^FTSE");
      // MISSING: dollar/yen
      // MISSING: dollar/swiss frank
      addSmaNormOHLC("^TYX");
      addSmaNormOHLC("^TNX");
      addSmaNormOHLC("^FVX");
      addSmaNormOHLC("^IRX");
      // MISSING: eurobond

      addSmaNormOHLC("^XAU");
      addSmaNorm("^XAU", x => x.Volume);

      // % Diff. between Normalized DJIA and Normalized T Bond
      bOnly.Add(get("^DJI").Closes().NormalizeUnit().ZipElements<Value, Value>(get("^TYX").Closes().NormalizeUnit(), (dj, tb, _) => dj[0] - tb[0]));

      Func<DataSeries<Value>, DataSeries<Value>> removeLastBar = s => new DataSeries<Value>(s.Symbol, s.Take(s.Length - 1));

      var unalignedData = SeriesToMatrix(aOnly.Concat(bOnly).Select(s => s.NormalizeUnit()).ToList());
      var unalignedOutput = SeriesToMatrix(List.Create(get("DIA").MapElements<Value>((s, _) => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close))));

      var data = MatrixFromColumns(unalignedData.Columns().Take(unalignedData.ColumnCount - 1).ToList());
      var output = MatrixFromColumns(unalignedOutput.Columns().Skip(1).ToList());

      int validationSize = 67;
      int trainingSize = output.ColumnCount - validationSize;
      TrainingInput = MatrixFromColumns(data.Columns().Take(trainingSize).ToList());
      ValidationInput = MatrixFromColumns(data.Columns().Skip(trainingSize).ToList());
      TrainingOutput = new DenseVector(output.Row(0).Take(trainingSize).ToArray());
      ValidationOutput = new DenseVector(output.Row(0).Skip(trainingSize).ToArray());
      DatabaseADimension = aOnly.Count;
    }

    public static Matrix MatrixFromColumns(List<Vector> columns)
    {
      var m = columns.First().Count;
      var n = columns.Count;
      
      Matrix X = new DenseMatrix(m, n);

      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = columns[j][i];

      return X;
    }

    public static List<Vector> Columns(this Matrix m)
    {
      return List.Repeat(m.ColumnCount, j => (Vector)m.Column(j));
    }

    public static List<Vector> Rows(this Matrix m)
    {
      return List.Repeat(m.RowCount, j => (Vector)m.Row(j));
    }

    public static Matrix SeriesToMatrix(List<DataSeries<Value>> series)
    {
      var m = series.Count;
      var n = series.First().Length;

      Matrix X = new DenseMatrix(m, n);
      for (int i = 0; i < m; i++)
        for (int j = 0; j < n; j++)
          X[i, j] = series[i][j];

      return X;
    }

    public static Vector ComplementCode(Vector input)
    {
      return new DenseVector(input.Concat(input.Select(x => 1.0 - x)).ToArray());
    }

    public static Matrix PrincipleComponents(Matrix data)
    {
      var rows = data.Rows();
      var meanAdjustedRows = rows.Select(x => (Vector)x.Subtract(x.Average())).ToList();
      var X = MatrixFromColumns(meanAdjustedRows); // we needed to transpose it anyway

      var svd = X.Svd(true);
      var V = (Matrix)svd.VT().Transpose();
      return V;
    }

    public static Vector NthPrincipleComponent(Matrix pcs, int n, Vector x)
    {
      var pc = pcs.Column(n);
      return (Vector)(x.DotProduct(pc) * pc);
    }

    static Random Random = new Random();
    public static VMixture Evolve()
    {
      var pop = List.Repeat(POPULATION_SIZE, n => new VMixture());
      for (int epoch = 0; epoch < EPOCH_COUNT; epoch++)
      {
        var selected = pop.OrderByDescending(m => m.Fitness()).Take(SELECTION_SIZE).ToList();
        pop = new List<VMixture>();
        for (int i = 0; i < POPULATION_SIZE / 2; i++)
        {
          var p1 = pop[Random.Next(SELECTION_SIZE)];
          var p2 = pop.Except(List.Create(p1)).ElementAt(Random.Next(SELECTION_SIZE - 1));
          pop.AddRange(p1.CrossoverAndMutate(p2));
        }
      }
      return pop.OrderByDescending(m => m.Fitness()).First();
    }

    public static void GetData()
    {
      var dir = @"c:\users\wintonpc\git\Quqe\Share\VersaceData";
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      foreach (var ticker in Tickers)
      {
        using (var c = new WebClient())
        {

          var fn = Path.Combine(dir, ticker + ".txt");


          if (ticker.StartsWith("^DJ")) // historical downloads of dow jones indices are not allowed
          {
            GetDowJonesData(c, ticker, fn);
          }
          else
          {
            var address = string.Format("http://ichart.finance.yahoo.com/table.csv?s={0}&a={1}&b={2}&c={3}&d={4}&e={5}&f={6}&g=d&ignore=.csv",
              ticker, StartDate.Month - 1, StartDate.Day, StartDate.Year, EndDate.Month - 1, EndDate.Day, EndDate.Year);
            c.DownloadFile(address, fn);
          }

          var fixedLines = File.ReadAllLines(fn).Skip(1).Reverse().Select(line => {
            var toks = line.Split(',');
            var timestamp = DateTime.ParseExact(toks[0], "yyyy-MM-dd", null);
            var open = double.Parse(toks[1]);
            var high = double.Parse(toks[2]);
            var low = double.Parse(toks[3]);
            var close = double.Parse(toks[4]);
            var volume = long.Parse(toks[5]);
            return string.Format("{0:yyyyMMdd};{1};{2};{3};{4};{5}",
              timestamp, open, high, low, close, volume);
          });
          File.WriteAllLines(fn, fixedLines);
        }
      }
    }

    private static void GetDowJonesData(WebClient c, string ticker, string fn)
    {
      List<string> lines = new List<string>();
      var address = string.Format("http://finance.yahoo.com/q/hp?s={0}&a={1}&b={2}&c={3}&d={4}&e={5}&f={6}&g=d",
        ticker, StartDate.Month - 1, StartDate.Day, StartDate.Year, EndDate.Month - 1, EndDate.Day, EndDate.Year);

      while (true)
      {
        var html = c.DownloadString(address);
        var trs = Regex.Matches(html, @"<tr[^>]*>(<td [^>]*tabledata[^>]*>[^<]+</td>)+</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
          .OfType<Match>().Select(m => m.Groups[0].Value).ToList();
        foreach (var tr in trs)
        {
          var fs = Regex.Matches(tr, @"<td [^>]*tabledata[^>]*>([^<]+)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline).OfType<Match>().Select(m => m.Groups[1].Value).ToList();
          if (fs.Count != 7)
            continue;
          var timestamp = DateTime.ParseExact(fs[0], "MMM d, yyyy", null);
          if (timestamp < StartDate) // yahoo enumerates data in reverse chronological order
            goto Done;
          lines.Add(string.Format("{0:yyyy-MM-dd},{1},{2},{3},{4},{5}",
            timestamp, double.Parse(fs[1]), double.Parse(fs[2]), double.Parse(fs[3]), double.Parse(fs[4]), long.Parse(fs[5], System.Globalization.NumberStyles.AllowThousands)));
        }

        var nextAddrMatch = Regex.Match(html, @"<a [^>]*href=""([^""]+)""[^>]*>Next</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!nextAddrMatch.Success)
          goto Done;
        address = "http://finance.yahoo.com" + HttpUtility.HtmlDecode(nextAddrMatch.Groups[1].Value);
      }
    Done: File.WriteAllLines(fn, lines);
    }
  }

  public abstract class VGene
  {
    public readonly string Name;
    public VGene(string name) { Name = name; }
    public abstract VGene Mutate();
    public abstract XElement ToXml();
    public abstract void SetXmlValue(string value);
  }

  public class VGene<TValue> : VGene
    where TValue : struct
  {
    public readonly double Min;
    public readonly double Max;
    public readonly double Granularity;
    public TValue Value { get; private set; }

    public VGene(string name, double min, double max, double granularity, TValue? initialValue = null)
      :base(name)
    {
      Min = min;
      Max = max;
      Granularity = granularity;
      Value = initialValue ?? RandomValue();
    }

    TValue RandomValue()
    {
      return (TValue)Convert.ChangeType(
          Optimizer.Quantize(Optimizer.RandomDouble(Min, Max), Min, Granularity),
          typeof(TValue));
    }

    Random Random = new Random();
    public override VGene Mutate()
    {
      return new VGene<TValue>(Name, Min, Max, Granularity, RandomValue());
    }

    public override XElement ToXml()
    {
      return new XElement("Gene", new XAttribute("Name", Name), new XAttribute("Value", Value));
    }

    public override void SetXmlValue(string value)
    {
      Value = (TValue)Convert.ChangeType(value, typeof(TValue));
    }
  }

  public class VChrom
  {
    public List<VGene> Genes;

    public VChrom()
    {
      Genes = new List<VGene> {
        new VGene<int>("NetworkType", 0, 1, 1),
        new VGene<int>("ElmanTrainingEpochs", 20, 1000, 1),
        new VGene<double>("ElmanLearningRate", 0.1, 0.3, 0.001),
        new VGene<int>("DatabaseType", 0, 1, 1),
        new VGene<double>("TrainingOffsetPct", 0, 1, 0.00001),
        new VGene<double>("TrainingSizePct", 0, 1, 0.00001),
        new VGene<int>("UseComplementCoding", 0, 1, 1),
        new VGene<int>("UsePrincipalComponentAnalysis", 0, 1, 1),
        new VGene<int>("PrincipalComponent", 0, 100, 1),
        new VGene<double>("RbfNetMinAccuracy", 0, 10, 0.1),
        new VGene<double>("RbfGaussianSpread", 0.1, 10, 0.01),
        new VGene<int>("ElmanHidden1NodeCount", 3, 200, 1),
        new VGene<int>("ElmanHidden2NodeCount", 3, 200, 1)
      };
    }

    VChrom(List<VGene> genes)
    {
      Genes = genes;
    }

    Random Random = new Random();
    public List<VChrom> CrossoverAndMutate(VChrom other)
    {
      var a = Genes.ToList();
      var b = other.Genes.ToList();
      for (int i=0; i<a.Count; i++)
        if (Random.Next(2) == 0)
        {
          var t = a[i];
          a[i] = b[i];
          b[i] = t;
        }

      Func<List<VGene>, List<VGene>> mutate = genes => genes.Select(g => Random.NextDouble() < Versace.MUTATION_RATE ? g.Mutate() : g).ToList();

      return List.Create(new VChrom(mutate(a)), new VChrom(mutate(b)));
    }

    TValue GetGeneValue<TValue>(string name) where TValue: struct
    {
      return ((VGene<TValue>)Genes.First(g => g.Name == name)).Value;
    }

    public NetworkType NetworkType { get { return GetGeneValue<int>("NetworkType") == 0 ? NetworkType.Elman : NetworkType.RBF; } }
    public int ElmanTrainingEpochs { get { return GetGeneValue<int>("ElmanTrainingEpochs"); } }
    public double ElmanLearningRate { get { return GetGeneValue<double>("ElmanLearningRate"); } }
    public DatabaseType DatabaseType { get { return GetGeneValue<int>("DatabaseType") == 0 ? DatabaseType.A : DatabaseType.B; } }
    public double TrainingOffsetPct { get { return GetGeneValue<double>("TrainingOffsetPct"); } }
    public double TrainingSizePct { get { return GetGeneValue<double>("TrainingSizePct"); } }
    public bool UseComplementCoding { get { return GetGeneValue<int>("UseComplementCoding") == 1; } }
    public bool UsePrincipalComponentAnalysis { get { return GetGeneValue<int>("UsePrincipalComponentAnalysis") == 1; } }
    public int PrincipalComponent { get { return GetGeneValue<int>("PrincipalComponent"); } }
    public double RbfNetMinAccuracy { get { return GetGeneValue<double>("RbfNetMinAccuracy"); } }
    public double RbfGaussianSpread { get { return GetGeneValue<double>("RbfGaussianSpread"); } }
    public int ElmanHidden1NodeCount { get { return GetGeneValue<int>("ElmanHidden1NodeCount"); } }
    public int ElmanHidden2NodeCount { get { return GetGeneValue<int>("ElmanHidden2NodeCount"); } }

    public XElement ToXml()
    {
      return new XElement("Chromosome", Genes.Select(x => x.ToXml()).ToArray());
    }

    public static VChrom FromXml(XElement e)
    {
      var c = new VChrom();
      foreach (var ge in e.Elements("Gene"))
        c.Genes.First(x => x.Name == ge.Attribute("Name").Value).SetXmlValue(ge.Attribute("Value").Value);
      return c;
    }
  }

  public enum NetworkType { Elman, RBF }
  public enum DatabaseType { A, B }

  public class VMixture
  {
    List<VChrom> Chromosomes;

    public VMixture()
    {
      Chromosomes = List.Repeat(Versace.EXPERTS_PER_MIXTURE, n => new VChrom());
    }

    VMixture(List<VChrom> chromosomes)
    {
      Chromosomes = chromosomes;
    }

    public List<VMixture> CrossoverAndMutate(VMixture other)
    {
      var q = Chromosomes.Zip(other.Chromosomes, (a, b) => a.CrossoverAndMutate(b));
      return List.Create(
        new VMixture(q.Select(x => x[0]).ToList()),
        new VMixture(q.Select(x => x[1]).ToList()));
    }

    internal double Fitness()
    {
      var experts = Chromosomes.Select(x => new Expert(x)).ToList();
      foreach (var x in experts)
        x.Train();
      int correctCount = 0;
      for (int j = 0; j < Versace.ValidationOutput.Count; j++)
      {
        var vote = Math.Sign(experts.Average(x => {
          return x.Predict(Versace.ValidationInput.Column(j).ToArray());
        }));
        Debug.Assert(vote != 0);
        if (Versace.ValidationOutput[j] == vote)
          correctCount++;
      }
      return (double)correctCount / Versace.ValidationOutput.Count;
    }

    public XElement ToXml()
    {
      return new XElement("Chromosomes", Chromosomes.Select(x => x.ToXml()).ToArray());
    }

    public static VMixture FromXml(string fn)
    {
      return new VMixture(XDocument.Load(fn).Element("Chromosomes").Elements("Chromosome").Select(x => VChrom.FromXml(x)).ToList());
    }
  }

  public interface IPredictor
  {
    double Predict(double[] input);
    void Reset();
  }

  public class Expert : IPredictor
  {
    VChrom Chromosome;
    IPredictor Network;
    Matrix PrincipalComponents;

    public Expert(VChrom chrom, Vector parameters, Matrix principalComponents)
    {
      Chromosome = chrom;
      int inputDimension = chrom.DatabaseType == DatabaseType.A ? Versace.DatabaseADimension : Versace.TrainingInput.RowCount;
      PrincipalComponents = principalComponents;

      if (chrom.NetworkType == NetworkType.Elman)
        Network = new ElmanNet(inputDimension, List.Create(chrom.ElmanHidden1NodeCount, chrom.ElmanHidden2NodeCount), 1);
      else
      {
        throw new NotImplementedException();
      }
    }

    public Expert(VChrom chrom)
    {
      Chromosome = chrom;
      int inputDimension = chrom.DatabaseType == DatabaseType.A ? Versace.DatabaseADimension : Versace.TrainingInput.RowCount;
      if (chrom.UseComplementCoding)
        inputDimension *= 2;
      if (chrom.NetworkType == NetworkType.Elman)
        Network = new ElmanNet(inputDimension, List.Create(chrom.ElmanHidden1NodeCount, chrom.ElmanHidden2NodeCount), 1);
      else
      {
        throw new NotImplementedException();
      }
    }

    public void Train()
    {
      var inputs = Versace.TrainingInput.Columns();
      if (Chromosome.UseComplementCoding)
        inputs = inputs.Select(x => Versace.ComplementCode(x)).ToList();
      if (Chromosome.UsePrincipalComponentAnalysis)
      {
        var pcs = Versace.PrincipleComponents(Versace.MatrixFromColumns(inputs));
        inputs = inputs.Select(x => Versace.NthPrincipleComponent(pcs, Chromosome.PrincipalComponent, x)).ToList();
      }

      if (Chromosome.NetworkType == NetworkType.Elman)
        ElmanNet.Train((ElmanNet)Network, Versace.MatrixFromColumns(inputs), Versace.TrainingOutput);
      else
      {
        throw new NotImplementedException();
      }
    }

    public double Predict(double[] input)
    {
      return Network.Predict(input);
    }

    public void Reset()
    {
      Network.Reset();
    }
  }
}
