using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PCW;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using MathNet.Numerics.LinearAlgebra.Generic;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Diagnostics;
using YamlDotNet.RepresentationModel.Serialization;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.Xml;
using System.Threading.Tasks;

namespace Quqe
{
  public class VersaceResult
  {
    public VMixture BestMixture;
    public List<double> FitnessHistory;

    public static string DoublesToBase64(IEnumerable<double> h)
    {
      using (var ms = new MemoryStream())
      using (var bw = new BinaryWriter(ms))
      {
        foreach (var d in h)
          bw.Write(d);
        return Convert.ToBase64String(ms.ToArray());
      }
    }

    public static List<double> DoublesFromBase64(string b)
    {
      var result = new List<double>();
      using (var ms = new MemoryStream(Convert.FromBase64String(b)))
      using (var br = new BinaryReader(ms))
      {
        while (ms.Position < ms.Length)
          result.Add(br.ReadDouble());
      }
      return result;
    }

    public void Save()
    {
      if (!Directory.Exists("VersaceResults"))
        Directory.CreateDirectory("VersaceResults");
      new XElement("VersaceResult",
        new XElement("FitnessHistory", DoublesToBase64(FitnessHistory)),
        BestMixture.ToXml())
        .Save(string.Format("VersaceResults\\VersaceResult-{0:yyyyMMdd}-{0:HHmmss}.xml", DateTime.Now));
    }

    public static VersaceResult Load(string fn)
    {
      var vr = XElement.Load(fn);
      return new VersaceResult {
        FitnessHistory = DoublesFromBase64(vr.Element("FitnessHistory").Value),
        BestMixture = VMixture.Load(vr.Element("Mixture"))
      };
    }
  }

  public static class Versace
  {
    public const int EXPERTS_PER_MIXTURE = 10;
    public const int POPULATION_SIZE = 10;
    public const int SELECTION_SIZE = 4;
    public const int EPOCH_COUNT = 100;
    //public const int EPOCH_COUNT = 1;
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

    public static Vector NthPrincipleComponent(Matrix principleComponents, int n, Vector x)
    {
      var pc = principleComponents.Column(n);
      return (Vector)(x.DotProduct(pc) * pc);
    }

    static Random Random = new Random();
    public static VersaceResult Evolve()
    {
      var fitnessHistory = new List<double>();
      var population = List.Repeat(POPULATION_SIZE, n => new VMixture());
      VMixture bestMixture = null;
      for (int epoch = 0; epoch < EPOCH_COUNT; epoch++)
      {
        Trace.WriteLine(string.Format("Epoch {0} started {1}", epoch, DateTime.Now));
        var experts = population.SelectMany(mixture => mixture.Members).Select(c => c.RefreshExpert()).ToList();

        int numTrained = 0;
        object trainLock = new object();
        Action trainedOne = () => {
          lock (trainLock)
          {
            numTrained++;
            Trace.WriteLine(string.Format("Epoch {0}, trained {1} / {2}", epoch, numTrained, experts.Count));
            if (numTrained % 10 == 0)
              GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
          }
        };

        bool parallelize = true;

        if (!parallelize)
        {
          RBFNet.ShouldTrace = true;
          RNN.ShouldTrace = true;
          foreach (var expert in experts)
          {
            expert.Train();
            trainedOne();
          }
        }
        else
        {
          RBFNet.ShouldTrace = false;
          RNN.ShouldTrace = false;
          // optimize training order to keep the most load on the CPUs
          var allExperts = population.SelectMany(mixture => mixture.Members).ToList();
          var rnnExperts = allExperts.Where(x => x.NetworkType == NetworkType.Elman).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseADimension : TrainingInput.RowCount);
            return x.ElmanHidden1NodeCount * x.ElmanHidden2NodeCount * x.ElmanTrainingEpochs * inputFactor;
          }).ToList();
          var rbfExperts = allExperts.Where(x => x.NetworkType == NetworkType.RBF).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseADimension : TrainingInput.RowCount);
            return x.TrainingSizePct * inputFactor;
          }).ToList();
          //var reordered = rnnExperts.Interleave(rbfExperts).ToList();
          var reordered = rnnExperts.Concat(rbfExperts).ToList();
          Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 8 },
            reordered
            .Select(c => c.RefreshExpert()).Select(expert => new Action(() => {
              expert.Train();
              trainedOne();
            })).ToArray());
        }

        foreach (var mixture in population)
          mixture.ComputeFitness();
        var oldPopulation = population.ToList();
        var selected = population.OrderByDescending(m => m.Fitness).Take(SELECTION_SIZE).ToList();
        population = new List<VMixture>();

        List.Repeat(POPULATION_SIZE / 2, i => {
          var parent1 = selected[Random.Next(SELECTION_SIZE)];
          var parent2 = selected.Except(List.Create(parent1)).ElementAt(Random.Next(SELECTION_SIZE - 1));
          population.AddRange(parent1.CrossoverAndMutate(parent2));
        });

        var bestThisEpoch = selected.First();
        if (bestMixture == null || bestThisEpoch.Fitness > bestMixture.Fitness)
          bestMixture = bestThisEpoch;
        fitnessHistory.Add(bestThisEpoch.Fitness);
        Trace.WriteLine(string.Format("Epoch {0} fitness:  {1:N1}%   (Best: {2:N1}%)", epoch, bestThisEpoch.Fitness * 100.0, bestMixture.Fitness * 100.0));
        var oldChromosomes = oldPopulation.SelectMany(m => m.Members).ToList();
        Trace.WriteLine(string.Format("Epoch {0} composition:   Elman {1:N1}%   RBF {2:N1}%", epoch,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.Elman) / oldChromosomes.Count * 100,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RBF) / oldChromosomes.Count * 100));
        Trace.WriteLine(string.Format("Epoch {0} ended {1}", epoch, DateTime.Now));
        GC.Collect();
        Trace.WriteLine("===========================================================================");
      }
      var result = new VersaceResult {
        BestMixture = bestMixture,
        FitnessHistory = fitnessHistory
      };
      result.Save();
      return result;
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
    public TValue Value { get; set; }

    public VGene(string name, double min, double max, double granularity, TValue? initialValue = null)
      : base(name)
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

    static Random Random = new Random();
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

  public class VMember
  {
    public List<VGene> Chromosome;
    public Expert Expert { get; private set; }

    public VMember()
    {
      Chromosome = new List<VGene> {
        new VGene<int>("NetworkType", 0, 1, 1),
        //new VGene<int>("NetworkType", 1, 1, 1),
        new VGene<int>("ElmanTrainingEpochs", 20, 200, 1),
        //new VGene<int>("ElmanTrainingEpochs", 20, 1000, 1),
        new VGene<int>("DatabaseType", 0, 1, 1),
        new VGene<double>("TrainingOffsetPct", 0, 1, 0.00001),
        new VGene<double>("TrainingSizePct", 0, 1, 0.00001),
        new VGene<int>("UseComplementCoding", 0, 1, 1),
        new VGene<int>("UsePrincipalComponentAnalysis", 0, 1, 1),
        new VGene<int>("PrincipalComponent", 0, 100, 1),
        new VGene<double>("RbfNetTolerance", 0, 1, 0.001),
        new VGene<double>("RbfGaussianSpread", 0.1, 10, 0.01),
        new VGene<int>("ElmanHidden1NodeCount", 3, 150, 1),
        new VGene<int>("ElmanHidden2NodeCount", 3, 100, 1)
        //new VGene<int>("ElmanHidden1NodeCount", 3, 200, 1),
        //new VGene<int>("ElmanHidden2NodeCount", 3, 200, 1)
      };
    }

    VMember(List<VGene> genes)
    {
      Chromosome = genes;
    }

    public Expert RefreshExpert()
    {
      Expert = new Expert(this);
      return Expert;
    }

    static Random Random = new Random();
    public List<VMember> CrossoverAndMutate(VMember other)
    {
      var a = Chromosome.ToList();
      var b = other.Chromosome.ToList();
      for (int i = 0; i < a.Count; i++)
        if (Random.Next(2) == 0)
        {
          var t = a[i];
          a[i] = b[i];
          b[i] = t;
        }

      Func<List<VGene>, List<VGene>> mutate = genes => genes.Select(g => Random.NextDouble() < Versace.MUTATION_RATE ? g.Mutate() : g).ToList();

      return List.Create(new VMember(mutate(a)), new VMember(mutate(b)));
    }

    TValue GetGeneValue<TValue>(string name) where TValue : struct
    {
      return ((VGene<TValue>)Chromosome.First(g => g.Name == name)).Value;
    }

    void SetGeneValue<TValue>(string name, TValue value) where TValue : struct
    {
      ((VGene<TValue>)Chromosome.First(g => g.Name == name)).Value = value;
    }

    public NetworkType NetworkType { get { return GetGeneValue<int>("NetworkType") == 0 ? NetworkType.Elman : NetworkType.RBF; } }
    public int ElmanTrainingEpochs { get { return GetGeneValue<int>("ElmanTrainingEpochs"); } }
    public DatabaseType DatabaseType { get { return GetGeneValue<int>("DatabaseType") == 0 ? DatabaseType.A : DatabaseType.B; } }
    public double TrainingOffsetPct { get { return GetGeneValue<double>("TrainingOffsetPct"); } }
    public double TrainingSizePct { get { return GetGeneValue<double>("TrainingSizePct"); } }
    public bool UseComplementCoding { get { return GetGeneValue<int>("UseComplementCoding") == 1; } }
    public bool UsePrincipalComponentAnalysis { get { return GetGeneValue<int>("UsePrincipalComponentAnalysis") == 1; } }
    public int PrincipalComponent { get { return GetGeneValue<int>("PrincipalComponent"); } }
    public double RbfNetTolerance { get { return GetGeneValue<double>("RbfNetTolerance"); } }
    public double RbfGaussianSpread
    {
      get { return GetGeneValue<double>("RbfGaussianSpread"); }
      set { SetGeneValue<double>("RbfGaussianSpread", value); }
    }
    public int ElmanHidden1NodeCount { get { return GetGeneValue<int>("ElmanHidden1NodeCount"); } }
    public int ElmanHidden2NodeCount { get { return GetGeneValue<int>("ElmanHidden2NodeCount"); } }

    public XElement ToXml()
    {
      return new XElement("Member",
        new XElement("Chromosome", Chromosome.Select(x => x.ToXml()).ToArray()),
        Expert.ToXml());
    }

    public static VMember Load(XElement eMember)
    {
      var member = new VMember();
      var eChrom = eMember.Element("Chromosome");
      foreach (var ge in eChrom.Elements("Gene"))
        member.Chromosome.First(x => x.Name == ge.Attribute("Name").Value).SetXmlValue(ge.Attribute("Value").Value);
      member.Expert = Expert.Load(eMember.Element("Expert"), member);
      return member;
    }
  }

  public enum NetworkType { Elman, RBF }
  public enum DatabaseType { A, B }

  public class VMixture : IPredictor
  {
    public List<VMember> Members { get; private set; }

    public VMixture()
    {
      Members = List.Repeat(Versace.EXPERTS_PER_MIXTURE, n => new VMember());
    }

    VMixture(List<VMember> members)
    {
      Members = members;
    }

    public List<VMixture> CrossoverAndMutate(VMixture other)
    {
      var q = Members.Zip(other.Members, (a, b) => a.CrossoverAndMutate(b));
      return List.Create(
        new VMixture(q.Select(x => x[0]).ToList()),
        new VMixture(q.Select(x => x[1]).ToList()));
    }

    public double Fitness { get; private set; }
    internal double ComputeFitness()
    {
      int correctCount = 0;
      var experts = Members.Select(x => x.Expert).ToList();
      foreach (var expert in experts)
        expert.Reset();
      for (int j = 0; j < Versace.ValidationOutput.Count; j++)
      {
        var vote = Predict(Versace.ValidationInput.Column(j));
        Debug.Assert(vote != 0);
        if (Versace.ValidationOutput[j] == vote)
          correctCount++;
      }
      Fitness = (double)correctCount / Versace.ValidationOutput.Count;
      return Fitness;
    }

    public double Predict(Vector<double> input)
    {
      return Math.Sign(Members.Select(x => x.Expert).Average(x => {
        double prediction = x.Predict(input);
        return prediction;
      }));
    }

    public void Reset()
    {
      foreach (var m in Members)
        m.Expert.Reset();
    }

    public XElement ToXml()
    {
      return new XElement("Mixture",
        new XAttribute("Fitness", Fitness),
        Members.Select(x => x.ToXml()).ToArray());
    }

    public static VMixture Load(XElement eMixture)
    {
      var mixture = new VMixture(eMixture.Elements("Member").Select(x => VMember.Load(x)).ToList());
      mixture.Fitness = double.Parse(eMixture.Attribute("Fitness").Value);
      return mixture;
    }

    public void Dump()
    {
      Trace.WriteLine("Mixture, fitness: " + Fitness);
      var members = Members.OrderBy(x => x.NetworkType).ToList();
      for (int i = 0; i < members.Count; i++)
      {
        var mi = members[i];
        var ss = new List<string>();
        ss.Add(mi.NetworkType.ToString());
        if (mi.NetworkType == NetworkType.Elman)
          ss.Add(string.Format("{0}-{1}-1:{2}", mi.ElmanHidden1NodeCount, mi.ElmanHidden2NodeCount, mi.ElmanTrainingEpochs));
        else
          ss.Add(string.Format("{0} centers, spread = {1}",
            ((RBFNet)mi.Expert.Network).NumCenters, ((RBFNet)mi.Expert.Network).Spread));
        ss.Add("Training set size = " + (int)(mi.TrainingSizePct * Versace.TrainingOutput.Count));
        if (mi.UseComplementCoding)
          ss.Add("CC");
        if (mi.UsePrincipalComponentAnalysis)
          ss.Add("PC=" + mi.PrincipalComponent);
        ss.Add("DB=" + mi.DatabaseType);
        Trace.WriteLine("Expert " + i + ": " + ss.Join(", "));
      }
    }
  }

  public interface IPredictor
  {
    double Predict(Vector<double> input);
    void Reset();
    XElement ToXml();
  }

  public class Expert : IPredictor
  {
    VMember Member;
    public IPredictor Network;
    Matrix PrincipalComponents;

    public Expert(VMember member)
    {
      Member = member;
    }

    List<Vector> Preprocess(List<Vector> inputs, bool recalculatePrincipalComponents = false)
    {
      // database selection
      if (Member.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => (Vector)x.SubVector(0, Versace.DatabaseADimension)).ToList();

      // complement coding
      if (Member.UseComplementCoding)
        inputs = inputs.Select(x => Versace.ComplementCode(x)).ToList();

      // PCA
      if (Member.UsePrincipalComponentAnalysis)
      {
        if (recalculatePrincipalComponents)
          PrincipalComponents = Versace.PrincipleComponents(Versace.MatrixFromColumns(inputs));
        var pcNumber = Math.Min(Member.PrincipalComponent, PrincipalComponents.ColumnCount - 1);
        inputs = inputs.Select(x => Versace.NthPrincipleComponent(PrincipalComponents, pcNumber, x)).ToList();
      }

      return inputs;
    }

    public void Train()
    {
      int offset = Math.Min((int)(Member.TrainingOffsetPct * Versace.TrainingInput.ColumnCount), Versace.TrainingInput.ColumnCount - 1);
      int size = Math.Max(1, Math.Min((int)(Member.TrainingSizePct * Versace.TrainingInput.ColumnCount), Versace.TrainingInput.ColumnCount - offset));
      var outputs = Versace.TrainingOutput.SubVector(offset, size);
      var inputs = Versace.TrainingInput.Columns().Skip(offset).Take(size).ToList(); // TODO: don't call Columns
      inputs = Preprocess(inputs, true);

      if (Member.NetworkType == NetworkType.Elman)
      {
        var rnn = new RNN(inputs.First().Count, new List<LayerSpec> {
          new LayerSpec {
            NodeCount = Member.ElmanHidden1NodeCount,
            ActivationType = ActivationType.LogisticSigmoid,
            IsRecurrent = true
          },
          new LayerSpec {
            NodeCount = Member.ElmanHidden2NodeCount,
            ActivationType = ActivationType.LogisticSigmoid,
            IsRecurrent = true
          },
          new LayerSpec {
            NodeCount = 1,
            ActivationType = ActivationType.Linear,
            IsRecurrent = false
          }
        });
        RNN.TrainSCG((RNN)rnn, Member.ElmanTrainingEpochs, Versace.MatrixFromColumns(inputs), outputs);
        Network = rnn;
      }
      else
      {
        double recommendedSpread;
        Network = RBFNet.Train(Versace.MatrixFromColumns(inputs), (Vector)outputs, Member.RbfNetTolerance, Member.RbfGaussianSpread, out recommendedSpread);
        Member.RbfGaussianSpread = recommendedSpread;
      }
    }

    public double Predict(Vector<double> input)
    {
      if (Network is RBFNet && ((RBFNet)Network).IsDegenerate)
        return 0;
      return Network.Predict(Preprocess(List.Create((Vector)input)).First());
    }

    public void Reset()
    {
      Network.Reset();
    }

    public XElement ToXml()
    {
      var eExpert = new XElement("Expert", Network.ToXml());
      if (PrincipalComponents != null)
        eExpert.Add(new XElement("PrincipalComponents",
          new XAttribute("RowCount", PrincipalComponents.RowCount),
          new XAttribute("ColumnCount", PrincipalComponents.ColumnCount),
          VersaceResult.DoublesToBase64(PrincipalComponents.ToColumnWiseArray())));
      return eExpert;
    }

    public static Expert Load(XElement eExpert, VMember member)
    {
      var ePc = eExpert.Element("PrincipalComponents");
      var eNetwork = eExpert.Element("Network");
      var expert = new Expert(member) {
        Network = eNetwork.Attribute("Type").Value == NetworkType.Elman.ToString()
          ? (IPredictor)RNN.Load(eNetwork) : (IPredictor)RBFNet.Load(eNetwork)
      };
      if (ePc != null)
        expert.PrincipalComponents = new DenseMatrix(
          int.Parse(ePc.Attribute("RowCount").Value),
          int.Parse(ePc.Attribute("ColumnCount").Value),
          VersaceResult.DoublesFromBase64(ePc.Value).ToArray());
      return expert;
    }
  }
}
