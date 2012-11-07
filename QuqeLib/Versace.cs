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
using System.Runtime;
using System.Reflection;
using System.Threading;

namespace Quqe
{
  public class VersaceResult
  {
    public string Path { get; private set; }
    public VMixture BestMixture;
    public List<double> FitnessHistory;
    public VersaceSettings VersaceSettings;

    public VersaceResult(VMixture bestMixture, List<double> fitnessHistory, VersaceSettings settings, string path = null)
    {
      BestMixture = bestMixture;
      FitnessHistory = fitnessHistory;
      VersaceSettings = settings;
      Path = path;
    }

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
      Path = string.Format("VersaceResults\\VersaceResult-{0:yyyyMMdd}-{0:HHmmss}.xml", DateTime.Now);
      new XElement("VersaceResult",
        new XElement("FitnessHistory", DoublesToBase64(FitnessHistory)),
        VersaceSettings.ToXml(),
        BestMixture.ToXml())
        .Save(Path);
    }

    public static VersaceResult Load(string fn)
    {
      var vr = XElement.Load(fn);
      return new VersaceResult(
        VMixture.Load(vr.Element("Mixture")),
        DoublesFromBase64(vr.Element("FitnessHistory").Value).ToList(),
        VersaceSettings.Load(vr.Element("VersaceSettings")),
        fn);
    }
  }

  public enum TrainingMethod { Evolve }
  public enum PredictionType { NextClose }
  public enum PreprocessingType { Enhanced }

  public class VersaceSettings
  {
    public string Path { get; private set; }

    public string PredictedSymbol = "DIA";
    public int ExpertsPerMixture = 10;
    public int PopulationSize = 10;
    public int SelectionSize = 4;
    public int EpochCount = 2;
    public double MutationRate = 0.05;
    public double MutationDamping = 0;
    public TrainingMethod TrainingMethod = TrainingMethod.Evolve;
    public PredictionType PredictionType = PredictionType.NextClose;
    public PreprocessingType PreprocessingType = PreprocessingType.Enhanced;

    public DateTime StartDate = DateTime.Parse("11/11/2001");
    public DateTime EndDate = DateTime.Parse("02/12/2003");
    public int TestingSplitPct = 78;
    public bool UseValidationSet = false;
    public int ValidationSplitPct = 0;

    public List<VGene> ProtoChromosome = new List<VGene> {
        new VGene<int>("NetworkType", 1, 1, 1),
        new VGene<int>("ElmanTrainingEpochs", 20, 20, 1),
        new VGene<int>("DatabaseType", 1, 1, 1),
        new VGene<double>("TrainingOffsetPct", 0, 1, 0.00001),
        new VGene<double>("TrainingSizePct", 0, 1, 0.00001),
        new VGene<int>("UseComplementCoding", 0, 1, 1),
        new VGene<int>("UsePrincipalComponentAnalysis", 0, 1, 1),
        new VGene<int>("PrincipalComponent", 0, 100, 1),
        new VGene<double>("RbfNetTolerance", 0, 1, 0.001),
        new VGene<double>("RbfGaussianSpread", 0.1, 10, 0.01),
        new VGene<int>("ElmanHidden1NodeCount", 3, 40, 1),
        new VGene<int>("ElmanHidden2NodeCount", 3, 20, 1)
      };

    public override string ToString()
    {
      var sb = new StringBuilder();
      foreach (var fi in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).Where(fi => fi.Name != "ProtoChromosome"))
        sb.AppendFormat("{0} : {1}\n", fi.Name, fi.GetValue(this));
      sb.AppendFormat("--------------------------\n");
      foreach (var g in ProtoChromosome)
        sb.AppendFormat("{0} : {1}\n", g.Name, g.RangeString);
      return sb.ToString().TrimEnd('\n');
    }

    public XElement ToXml()
    {
      return new XElement("VersaceSettings",
        new XAttribute("PredictedSymbol", PredictedSymbol),
        new XAttribute("ExpertsPerMixture", ExpertsPerMixture),
        new XAttribute("PopulationSize", PopulationSize),
        new XAttribute("SelectionSize", SelectionSize),
        new XAttribute("EpochCount", EpochCount),
        new XAttribute("MutationRate", MutationRate),
        new XAttribute("MutationDamping", MutationDamping),
        new XAttribute("StartDate", StartDate),
        new XAttribute("EndDate", EndDate),
        new XAttribute("TestingSplitPct", TestingSplitPct),
        new XAttribute("UseValidationSet", UseValidationSet),
        new XAttribute("ValidationSplitPct", ValidationSplitPct),
        new XAttribute("TrainingMethod", TrainingMethod),
        new XAttribute("PredictionType", PredictionType),
        new XAttribute("PreprocessingType", PreprocessingType),
        new XElement("ProtoChromosome", ProtoChromosome.Select(x => x.ToXml()).ToArray()));
    }

    public static VersaceSettings Load(string fn)
    {
      var vs = Load(XElement.Load(fn));
      vs.Path = fn;
      return vs;
    }

    public static VersaceSettings Load(XElement eSettings)
    {
      return new VersaceSettings {
        PredictedSymbol = eSettings.Attribute("PredictedSymbol").Value,
        ExpertsPerMixture = int.Parse(eSettings.Attribute("ExpertsPerMixture").Value),
        PopulationSize = int.Parse(eSettings.Attribute("PopulationSize").Value),
        SelectionSize = int.Parse(eSettings.Attribute("SelectionSize").Value),
        EpochCount = int.Parse(eSettings.Attribute("EpochCount").Value),
        MutationRate = double.Parse(eSettings.Attribute("MutationRate").Value),
        MutationDamping = double.Parse(eSettings.Attribute("MutationDamping").Value),
        StartDate = DateTime.Parse(eSettings.Attribute("StartDate").Value),
        EndDate = DateTime.Parse(eSettings.Attribute("EndDate").Value),
        TestingSplitPct = int.Parse(eSettings.Attribute("TestingSplitPct").Value),
        UseValidationSet = bool.Parse(eSettings.Attribute("UseValidationSet").Value),
        ValidationSplitPct = int.Parse(eSettings.Attribute("ValidationSplitPct").Value),
        TrainingMethod = (TrainingMethod)Enum.Parse(typeof(TrainingMethod), eSettings.Attribute("TrainingMethod").Value),
        PredictionType = (PredictionType)Enum.Parse(typeof(PredictionType), eSettings.Attribute("PredictionType").Value),
        PreprocessingType = (PreprocessingType)Enum.Parse(typeof(PreprocessingType), eSettings.Attribute("PreprocessingType").Value),
        ProtoChromosome = eSettings.Element("ProtoChromosome").Elements("Gene").Select(x => VGene.Load(x)).ToList()
      };
    }

    public DateTime TrainingStart { get { return StartDate; } }
    public DateTime TrainingEnd { get { return ValidationStart.AddDays(-1); } }
    public DateTime ValidationStart { get { return !UseValidationSet ? TestingStart : StartDate.AddMilliseconds(TestingStart.Subtract(StartDate).TotalMilliseconds * (double)ValidationSplitPct / 100).Date; } }
    public DateTime ValidationEnd { get { return TestingStart.AddDays(-1); } }
    public DateTime TestingStart { get { return StartDate.AddMilliseconds(EndDate.Subtract(StartDate).TotalMilliseconds * (double)TestingSplitPct / 100).Date; } }
    public DateTime TestingEnd { get { return EndDate; } }

    public VersaceSettings Clone()
    {
      return Load(ToXml());
    }
  }

  public static class Versace
  {
    static VersaceSettings _Settings;
    public static VersaceSettings Settings
    {
      get { return _Settings; }
      set
      {
        if (value == null)
        {
          TrainingInput = ValidationInput = TestingInput = null;
          TrainingOutput = ValidationOutput = TestingOutput = null;
        }
        else
        {
          _Settings = value;
          LoadNormalizedValues();
        }
      }
    }

    public static Dictionary<PreprocessingType, int> DatabaseAInputLength = new Dictionary<PreprocessingType, int>();

    public static List<string> GetTickers(string predictedSymbol)
    {
      return List.Create(predictedSymbol, "^IXIC", "^GSPC", "^DJI", "^DJT", "^DJU", "^DJA", "^N225", "^BVSP",
        "^GDAX", "^FTSE", /*"^CJJ", "USDCHF"*/ "^TYX", "^TNX", "^FVX", "^IRX", /*"EUROD"*/ "^XAU");
    }

    static Versace()
    {
      Settings = new VersaceSettings();
    }

    public static List<DataSeries<Bar>> GetCleanSeries(string predictedSymbol, List<string> tickers)
    {
      var raw = tickers.Select(t => Data.LoadVersace(t)).ToList();
      var dia = raw.First(s => s.Symbol == predictedSymbol);
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
    public static Matrix TestingInput { get; private set; }
    public static Vector TrainingOutput { get; private set; }
    public static Vector ValidationOutput { get; private set; }
    public static Vector TestingOutput { get; private set; }

    public static Func<DataSeries<Bar>, double> GetIdealSignalFunc(PredictionType pt)
    {
      if (pt == PredictionType.NextClose)
        return s => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close);
      else
        throw new Exception("Unexpected PredictionType: " + pt);
    }

    public static void LoadNormalizedValues()
    {
      Func<DataSeries<Bar>, double> idealSignal = GetIdealSignalFunc(Settings.PredictionType);

      PreprocessedData trainingData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.TrainingStart, Settings.TrainingEnd, true, idealSignal);
      TrainingInput = trainingData.Inputs;
      TrainingOutput = trainingData.Outputs;

      if (Settings.UseValidationSet)
      {
        PreprocessedData validationData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.ValidationStart, Settings.ValidationEnd, true, idealSignal);
        ValidationInput = validationData.Inputs;
        ValidationOutput = validationData.Outputs;
      }

      PreprocessedData testingData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.TestingStart, Settings.TestingEnd, true, idealSignal);
      TestingInput = testingData.Inputs;
      TestingOutput = testingData.Outputs;
    }

    //public static void LoadNormalizedValues()
    //{
    //  DateTime testingStart = Settings.StartDate.AddMilliseconds(Settings.EndDate.Subtract(Settings.StartDate).TotalMilliseconds * (double)Settings.TestingSplitPct / 100).Date;
    //  DateTime validationStart = testingStart;
    //  if (Settings.UseValidationSet)
    //    validationStart = Settings.StartDate.AddMilliseconds(testingStart.Subtract(Settings.StartDate).TotalMilliseconds * (double)Settings.ValidationSplitPct / 100).Date;

    //  Func<DataSeries<Bar>, double> idealSignal = null;
    //  if (Settings.PredictionType == PredictionType.NextClose)
    //    idealSignal = s => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close);

    //  PreprocessedData trainingData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, Settings.StartDate, validationStart.AddDays(-1), true, idealSignal);
    //  TrainingInput = trainingData.Inputs;
    //  TrainingOutput = trainingData.Outputs;

    //  if (Settings.UseValidationSet)
    //  {
    //    PreprocessedData validationData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, validationStart, testingStart.AddDays(-1), true, idealSignal);
    //    ValidationInput = validationData.Inputs;
    //    ValidationOutput = validationData.Outputs;
    //  }

    //  PreprocessedData testingData = GetPreprocessedValues(Settings.PreprocessingType, Settings.PredictedSymbol, testingStart, Settings.EndDate, true, idealSignal);
    //  TestingInput = testingData.Inputs;
    //  TestingOutput = testingData.Outputs;
    //}

    public static PreprocessedData GetPreprocessedValues(PreprocessingType preprocessType, string predictedSymbol, DateTime startDate, DateTime endDate, bool includeOutputs, Func<DataSeries<Bar>, double> idealSignal = null)
    {
      if (preprocessType == PreprocessingType.Enhanced)
        return PreprocessEnhanced(predictedSymbol, startDate, endDate, includeOutputs, idealSignal);
      else
        throw new Exception("Unexpected PreprocessingType: " + preprocessType);
    }

    static PreprocessedData PreprocessEnhanced(string predictedSymbol, DateTime startDate, DateTime endDate, bool includeOutputs, Func<DataSeries<Bar>, double> idealSignal)
    {
      var clean = GetCleanSeries(predictedSymbol, GetTickers(predictedSymbol));
      var aOnly = new List<DataSeries<Value>>();
      var bOnly = new List<DataSeries<Value>>();

      if (includeOutputs)
      {
        // increase endDate by one trading day, because on the true endDate,
        // we need to look one day ahead to know the correct prediction.
        // if the trueEnd date is the last day in the DataSeries (can't look ahead), we'll have to chop it off in the output.
        var ds = clean.First(); // any will do, since we already cleaned them to have the exact same timestamp sequence
        int i = 1;
        while (i < ds.Length && ds[i].Timestamp < endDate.AddDays(1))
          i++;
        endDate = ds[i].Timestamp;
      }

      /////////////////////
      // helper functions
      Func<string, DataSeries<Bar>> get = ticker => clean.First(x => x.Symbol == ticker)
        .From(startDate).To(endDate);

      Action<string, Func<Bar, Value>> addSmaNorm = (ticker, getValue) =>
        aOnly.Add(get(ticker).NormalizeSma10(getValue));

      Action<string> addSmaNormOHLC = (ticker) => {
        addSmaNorm(ticker, x => x.Open);
        addSmaNorm(ticker, x => x.High);
        addSmaNorm(ticker, x => x.Low);
        addSmaNorm(ticker, x => x.Close);
      };

      //////////////////////////////////////////
      // apply indicators and SMA normalizations
      var predicted = get(predictedSymbol);

      // % ROC Close
      bOnly.Add(predicted.MapElements<Value>((s, v) => {
        if (s.Pos == 0)
          return 0;
        else
          return (s[0].Close - s[1].Close) / s[1].Close * 100;
      }));

      // % Diff Open-Close
      bOnly.Add(predicted.MapElements<Value>((s, v) => (s[0].Open - s[0].Close) / s[0].Open * 100));

      // % Diff High-Low
      bOnly.Add(predicted.MapElements<Value>((s, v) => (s[0].High - s[0].Low) / s[0].Low * 100));

      // my own LinReg stuff
      {
        var fast = predicted.Closes().LinReg(2, 1);
        var slow = predicted.Closes().LinReg(7, 1);
        bOnly.Add(fast.ZipElements<Value, Value>(slow, (f, s, _) => f[0] - s[0]));
        bOnly.Add(predicted.Closes().RSquared(10));
        bOnly.Add(predicted.Closes().LinRegSlope(4));
      }

      addSmaNormOHLC(predictedSymbol);
      addSmaNorm(predictedSymbol, x => x.Volume);

      bOnly.Add(predicted.ChaikinVolatility(10));
      bOnly.Add(predicted.Closes().MACD(10, 21));
      bOnly.Add(predicted.Closes().Momentum(10));
      bOnly.Add(predicted.VersaceRSI(10));

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

      DatabaseAInputLength[PreprocessingType.Enhanced] = aOnly.Count;

      var unalignedData = SeriesToMatrix(aOnly.Concat(bOnly).Select(s => s.NormalizeUnit()).ToList());
      if (!includeOutputs)
      {
        return new PreprocessedData {
          Predicted = predicted,
          Inputs = unalignedData,
          Outputs = null
        };
      }
      //var unalignedOutput = SeriesToMatrix(List.Create(predicted.MapElements<Value>((s, _) => s.Pos == 0 ? 0 : Math.Sign(s[0].Close - s[1].Close))));
      var unalignedOutput = SeriesToMatrix(List.Create(predicted.MapElements<Value>((s, _) => idealSignal(s))));

      var data = MatrixFromColumns(unalignedData.Columns().Take(unalignedData.ColumnCount - 1).ToList());
      var output = MatrixFromColumns(unalignedOutput.Columns().Skip(1).ToList());
      return new PreprocessedData {
        Predicted = predicted,
        Inputs = data,
        Outputs = new DenseVector(output.Row(0).ToArray())
      };
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

    public static void Train(Action<List<double>> historyChanged, Action<VersaceResult> whenDone)
    {
      SyncContext sync = SyncContext.Current;
      Thread t = new Thread(() => {
        if (Settings.TrainingMethod == TrainingMethod.Evolve)
        {
          var result = Versace.Evolve(historyChanged);
          sync.Post(() => whenDone(result));
        }
      });
      t.IsBackground = true;
      t.Start();
    }

    static Random Random = new Random();
    public static VersaceResult Evolve(Action<List<double>> historyChanged = null)
    {
      GCSettings.LatencyMode = GCLatencyMode.Batch;
      var fitnessHistory = new List<double>();
      var population = List.Repeat(Settings.PopulationSize, n => new VMixture());
      VMixture bestMixture = null;
      for (int epoch = 0; epoch < Settings.EpochCount; epoch++)
      {
        Trace.WriteLine(string.Format("Epoch {0} started {1}", epoch, DateTime.Now));

        int numTrained = 0;
        object trainLock = new object();
        Action trainedOne = () => {
          lock (trainLock)
          {
            numTrained++;
            Trace.WriteLine(string.Format("Epoch {0}, trained {1} / {2}", epoch, numTrained, Settings.ExpertsPerMixture * Settings.PopulationSize));
            if (numTrained % 10 == 0)
              GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
          }
        };

        bool parallelize = true;

        if (!parallelize)
        {
          RBFNet.ShouldTrace = true;
          RNN.ShouldTrace = true;
          foreach (var expert in population.Where(mixture => mixture.Fitness == 0)
            .SelectMany(mixture => mixture.Members).Select(c => c.RefreshExpert()))
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
          var allUntrainedExperts = population.Where(mixture => mixture.Fitness == 0).SelectMany(mixture => mixture.Members).ToList();
          var rnnExperts = allUntrainedExperts.Where(x => x.NetworkType == NetworkType.RNN).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseAInputLength[Settings.PreprocessingType] : TrainingInput.RowCount);
            return x.ElmanHidden1NodeCount * x.ElmanHidden2NodeCount * x.ElmanTrainingEpochs * inputFactor;
          }).ToList();
          var rbfExperts = allUntrainedExperts.Where(x => x.NetworkType == NetworkType.RBF).OrderByDescending(x => {
            var inputFactor = (x.UseComplementCoding ? 2 : 1) * (x.DatabaseType == DatabaseType.A ? DatabaseAInputLength[Settings.PreprocessingType] : TrainingInput.RowCount);
            return x.TrainingSizePct * inputFactor;
          }).ToList();
          var reordered = rnnExperts.Concat(rbfExperts).ToList();
          Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 8 },
            reordered
            .Select(c => c.RefreshExpert()).Select(expert => new Action(() => {
              expert.Train();
              //expert.TrainEx(rnnTrialCount: 3);
              trainedOne();
            })).ToArray());
        }

        foreach (var mixture in population.Where(mixture => mixture.Fitness == 0))
          mixture.ComputeAndSetFitness();
        var oldPopulation = population.ToList();
        var rankedPopulation = population.OrderByDescending(m => m.Fitness).ToList();
        var selected = rankedPopulation.Take(Settings.SelectionSize).Shuffle().ToList();
        population = rankedPopulation.Take(Settings.PopulationSize - Settings.SelectionSize).ToList();
        //population = new List<VMixture>();

        Debug.Assert(selected.Count % 2 == 0);
        for (int i = 0; i < selected.Count; i += 2)
          population.AddRange(selected[i].Crossover(selected[i + 1]));

        //for (int i = 0; i < Settings.PopulationSize / 2; i++)
        //{
        //  var a = selected.RandomItem();
        //  var b = selected.Except(List.Create(a)).RandomItem();
        //  population.AddRange(a.CrossoverAndMutate(b));
        //}

        //// mutate the very best just a little bit for some variation
        //for (int i = 0; i < SELECTION_SIZE / 2; i++)
        //  population[i] = population[i].Mutate(mutationRate: 0.5, dampingFactor: 4);
        //// mutate the rest entirely randomly
        //for (int i = SELECTION_SIZE / 2; i < population.Count; i++)
        //  population[i] = population[i].Mutate(dampingFactor: 0);

        population = population.Select(x => x.Mutate(dampingFactor: 0)).ToList();

        if ((epoch + 1) % 4 == 0)
        {
          Func<VMember, bool> isGoodRbf = m => m.NetworkType == NetworkType.RBF && m.Expert != null && !((RBFNet)m.Expert.Network).IsDegenerate;
          foreach (var mixture in population.ToList())
          {
            var otherGood = oldPopulation.Except(List.Create(mixture)).SelectMany(mix => mix.Members.Where(isGoodRbf)).ToList();
            foreach (var member in mixture.Members.ToList())
            {
              if (!isGoodRbf(member))
              {
                mixture.Members.Remove(member);
                VMember otherGoodClone;
                if (!otherGood.Any())
                  otherGoodClone = new VMixture().Members.First();
                else
                  otherGoodClone = VMember.Load(otherGood.RandomItem().ToXml());
                mixture.Members.Add(otherGoodClone);
              }
            }
          }
        }

        var bestThisEpoch = rankedPopulation.First();
        if (bestMixture == null || bestThisEpoch.Fitness > bestMixture.Fitness)
          bestMixture = bestThisEpoch;
        fitnessHistory.Add(bestThisEpoch.Fitness);
        if (historyChanged != null)
          historyChanged(fitnessHistory);
        Trace.WriteLine(string.Format("Epoch {0} fitness:  {1:N1}%   (Best: {2:N1}%)", epoch, bestThisEpoch.Fitness * 100.0, bestMixture.Fitness * 100.0));
        var oldChromosomes = oldPopulation.SelectMany(m => m.Members).ToList();
        Trace.WriteLine(string.Format("Epoch {0} composition:   Elman {1:N1}%   RBF {2:N1}%", epoch,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RNN) / oldChromosomes.Count * 100,
          (double)oldChromosomes.Count(x => x.NetworkType == NetworkType.RBF) / oldChromosomes.Count * 100));
        Trace.WriteLine(string.Format("Epoch {0} ended {1}", epoch, DateTime.Now));
        GC.Collect();
        Trace.WriteLine("===========================================================================");
      }
      var result = new VersaceResult(bestMixture, fitnessHistory, Versace.Settings);
      result.Save();
      return result;
    }

    public static void GetData(string predictedSymbol, DateTime start, DateTime end)
    {
      var dir = @"c:\users\wintonpc\git\Quqe\Share\VersaceData";
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      foreach (var ticker in GetTickers(predictedSymbol))
      {
        using (var c = new WebClient())
        {
          var fn = Path.Combine(dir, ticker + ".txt");

          if (ticker.StartsWith("^DJ")) // historical downloads of dow jones indices are not allowed
          {
            GetDowJonesData(c, ticker, fn, start, end);
          }
          else
          {
            var address = string.Format("http://ichart.finance.yahoo.com/table.csv?s={0}&a={1}&b={2}&c={3}&d={4}&e={5}&f={6}&g=d&ignore=.csv",
              ticker, start.Month - 1, start.Day, start.Year, end.Month - 1, end.Day, end.Year);
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

    static void GetDowJonesData(WebClient c, string ticker, string fn, DateTime start, DateTime end)
    {
      List<string> lines = new List<string>();
      var address = string.Format("http://finance.yahoo.com/q/hp?s={0}&a={1}&b={2}&c={3}&d={4}&e={5}&f={6}&g=d",
        ticker, start.Month - 1, start.Day, start.Year, end.Month - 1, end.Day, end.Year);

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
          if (timestamp < Settings.StartDate) // yahoo enumerates data in reverse chronological order
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

  public class PreprocessedData
  {
    public DataSeries<Bar> Predicted;
    public Matrix Inputs;
    public Vector Outputs;
  }

  public abstract class VGene
  {
    public readonly string Name;
    public VGene(string name) { Name = name; }
    public abstract string RangeString { get; }
    public abstract VGene Clone();
    public abstract VGene CloneAndRandomize();
    public abstract VGene Mutate(double dampingFactor);
    public abstract XElement ToXml();

    static List<string> DoubleNames = List.Create("TrainingOffsetPct", "TrainingSizePct", "RbfNetTolerance", "RbfGaussianSpread");

    public static VGene Load(XElement eGene)
    {
      var name = eGene.Attribute("Name").Value;
      var min = double.Parse(eGene.Attribute("Min").Value);
      var max = double.Parse(eGene.Attribute("Max").Value);
      var granularity = double.Parse(eGene.Attribute("Granularity").Value);
      var valueString = eGene.Attribute("Value").Value;

      if (DoubleNames.Contains(name))
        return new VGene<double>(name, min, max, granularity, double.Parse(valueString));
      else
        return new VGene<int>(name, min, max, granularity, int.Parse(valueString));
    }
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

    public override string RangeString
    {
      get
      {
        if (Value is int)
        {
          if (Min == Max)
            return Min.ToString();
          else if (Min == 0 && Max == 1 && Granularity == 1)
            return "0/1";
          else
            return string.Format("{0} - {1}", Min, Max);
        }
        else
          return string.Format("{0:N1} - {1:N1}", Min, Max);
      }
    }

    TValue RandomValue()
    {
      return (TValue)Convert.ChangeType(
          Optimizer.Quantize(Optimizer.RandomDouble(Min, Max), Min, Granularity),
          typeof(TValue));
    }

    static Random Random = new Random();
    public override VGene Mutate(double dampingFactor)
    {
      //return new VGene<TValue>(Name, Min, Max, Granularity, RandomValue());
      var doubleValue = (double)Convert.ChangeType(Value, typeof(double));
      if (Min == 0 && Max == 1 && Value is int)
      {
        if (dampingFactor == 0 || Optimizer.WithProb(1 / dampingFactor))
          doubleValue = 1 - doubleValue;
        return new VGene<TValue>(Name, Min, Max, Granularity, (TValue)Convert.ChangeType(doubleValue, typeof(TValue)));
      }
      else
      {
        var rand = Optimizer.RandomDouble(Min, Max);
        var weighted = (dampingFactor * doubleValue + rand) / (dampingFactor + 1);
        var quantized = (TValue)Convert.ChangeType(
            Optimizer.Quantize(weighted, Min, Granularity),
            typeof(TValue));
        return new VGene<TValue>(Name, Min, Max, Granularity, quantized);
      }
    }

    public override VGene Clone()
    {
      return new VGene<TValue>(Name, Min, Max, Granularity, Value);
    }

    public override VGene CloneAndRandomize()
    {
      return new VGene<TValue>(Name, Min, Max, Granularity);
    }

    public override XElement ToXml()
    {
      return new XElement("Gene",
        new XAttribute("Name", Name),
        new XAttribute("Value", Value),
        new XAttribute("Min", Min),
        new XAttribute("Max", Max),
        new XAttribute("Granularity", Granularity)
        );
    }
  }

  public class VMember
  {
    public List<VGene> Chromosome;
    public Expert Expert { get; private set; }

    public VMember(Func<string, VGene> makeGene)
    {
      Chromosome = new List<VGene> {
        makeGene("NetworkType"),
        makeGene("ElmanTrainingEpochs"),
        makeGene("DatabaseType"),
        makeGene("TrainingOffsetPct"),
        makeGene("TrainingSizePct"),
        makeGene("UseComplementCoding"),
        makeGene("UsePrincipalComponentAnalysis"),
        makeGene("PrincipalComponent"),
        makeGene("RbfNetTolerance"),
        makeGene("RbfGaussianSpread"),
        makeGene("ElmanHidden1NodeCount"),
        makeGene("ElmanHidden2NodeCount")
      };
    }

    VMember(List<VGene> genes)
    {
      Chromosome = genes;
    }

    public Expert RefreshExpert()
    {
      Expert = new Expert(this, Versace.Settings.PreprocessingType);
      return Expert;
    }

    static Random Random = new Random();
    public List<VMember> CrossoverAndMutate(VMember other)
    {
      var a = Chromosome.ToList();
      var b = other.Chromosome.ToList();
      Crossover(a, b);

      return List.Create(new VMember(Mutate(a)), new VMember(Mutate(b)));
    }

    public List<VMember> Crossover(VMember other)
    {
      return Crossover(Chromosome, other.Chromosome).Select(c => new VMember(c)).ToList();
    }

    static List<List<VGene>> Crossover(IEnumerable<VGene> x, IEnumerable<VGene> y)
    {
      var a = x.ToList();
      var b = y.ToList();
      for (int i = 0; i < a.Count; i++)
        if (Random.Next(2) == 0)
        {
          var t = a[i];
          a[i] = b[i];
          b[i] = t;
        }
      return List.Create(a, b);
    }

    public VMember Mutate(double? mutationRate = null, double? dampingFactor = null)
    {
      mutationRate = mutationRate ?? Versace.Settings.MutationRate;
      dampingFactor = dampingFactor ?? Versace.Settings.MutationDamping;
      return new VMember(Mutate(Chromosome, mutationRate.Value, dampingFactor.Value));
    }

    static List<VGene> Mutate(IEnumerable<VGene> genes, double? mutationRate = null, double? dampingFactor = null)
    {
      mutationRate = mutationRate ?? Versace.Settings.MutationRate;
      dampingFactor = dampingFactor ?? Versace.Settings.MutationDamping;
      return genes.Select(g => Random.NextDouble() < mutationRate.Value ? g.Mutate(dampingFactor.Value) : g).ToList();
    }

    TValue GetGeneValue<TValue>(string name) where TValue : struct
    {
      return ((VGene<TValue>)Chromosome.First(g => g.Name == name)).Value;
    }

    void SetGeneValue<TValue>(string name, TValue value) where TValue : struct
    {
      ((VGene<TValue>)Chromosome.First(g => g.Name == name)).Value = value;
    }

    public NetworkType NetworkType { get { return GetGeneValue<int>("NetworkType") == 0 ? NetworkType.RNN : NetworkType.RBF; } }
    public int ElmanTrainingEpochs { get { return GetGeneValue<int>("ElmanTrainingEpochs"); } }
    public DatabaseType DatabaseType { get { return GetGeneValue<int>("DatabaseType") == 0 ? DatabaseType.A : DatabaseType.B; } }
    public double TrainingOffsetPct { get { return GetGeneValue<double>("TrainingOffsetPct"); } }
    public double TrainingSizePct { get { return GetGeneValue<double>("TrainingSizePct"); } }
    public bool UseComplementCoding { get { return GetGeneValue<int>("UseComplementCoding") == 1; } }
    public bool UsePrincipalComponentAnalysis { get { return GetGeneValue<int>("UsePrincipalComponentAnalysis") == 1; } }
    public int PrincipalComponent { get { return GetGeneValue<int>("PrincipalComponent"); } }
    public double RbfNetTolerance { get { return GetGeneValue<double>("RbfNetTolerance"); } }
    public double RbfGaussianSpread { get { return GetGeneValue<double>("RbfGaussianSpread"); } }
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
      var eChrom = eMember.Element("Chromosome");
      var eGenes = eChrom.Elements("Gene");
      var member = new VMember(name => VGene.Load(eGenes.First(x => x.Attribute("Name").Value == name)));
      member.Expert = Expert.Load(eMember.Element("Expert"), member);
      return member;
    }
  }

  public enum NetworkType { RNN, RBF }
  public enum DatabaseType { A, B }

  public class VMixture : IPredictor
  {
    public List<VMember> Members { get; private set; }

    public VMixture()
    {
      Members = List.Repeat(Versace.Settings.ExpertsPerMixture, n =>
        new VMember(name => Versace.Settings.ProtoChromosome.First(x => x.Name == name).CloneAndRandomize()));
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

    public List<VMixture> Crossover(VMixture other)
    {
      var q = Members.Zip(other.Members, (a, b) => a.Crossover(b));
      return List.Create(
        new VMixture(q.Select(x => x[0]).ToList()),
        new VMixture(q.Select(x => x[1]).ToList()));
    }

    public VMixture Mutate(double? mutationRate = null, double? dampingFactor = null)
    {
      mutationRate = mutationRate ?? Versace.Settings.MutationRate;
      dampingFactor = dampingFactor ?? Versace.Settings.MutationDamping;
      return new VMixture(Members.Select(x => x.Mutate(mutationRate, dampingFactor)).ToList());
    }

    public double Fitness { get; private set; }
    public static double ComputeFitness(IPredictor predictor)
    {
      int correctCount = 0;
      predictor.Reset();
      for (int j = 0; j < Versace.ValidationOutput.Count; j++)
      {
        var vote = Math.Sign(predictor.Predict(Versace.ValidationInput.Column(j)));
        Debug.Assert(vote != 0);
        if (Versace.ValidationOutput[j] == vote)
          correctCount++;
      }
      return (double)correctCount / Versace.ValidationOutput.Count;
    }

    public void ComputeAndSetFitness()
    {
      Fitness = ComputeFitness(this);
    }

    public double Predict(Vector<double> input)
    {
      return Math.Sign(Members.Select(x => x.Expert).Average(x => {
        double prediction = x.Predict(input);
        return double.IsNaN(prediction) ? 0 : prediction;
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
        if (mi.NetworkType == NetworkType.RNN)
          ss.Add(string.Format("{0}-{1}-1:{2}", mi.ElmanHidden1NodeCount, mi.ElmanHidden2NodeCount, mi.ElmanTrainingEpochs));
        else
        {
          ss.Add(string.Format("{0} centers, spread = {1}",
            ((RBFNet)mi.Expert.Network).NumCenters, ((RBFNet)mi.Expert.Network).Spread));
          if (((RBFNet)mi.Expert.Network).IsDegenerate)
            ss.Add("!! DEGENERATE !!");
        }
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
    PreprocessingType PreprocessingType;

    public Expert(VMember member, PreprocessingType preprocessType)
    {
      Member = member;
      PreprocessingType = preprocessType;
    }

    List<Vector> Preprocess(List<Vector> inputs, bool recalculatePrincipalComponents = false)
    {
      // database selection
      if (Member.DatabaseType == DatabaseType.A)
        inputs = inputs.Select(x => (Vector)x.SubVector(0, Versace.DatabaseAInputLength[PreprocessingType])).ToList();

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

    public void Train() { TrainEx(); }

    public void TrainEx(int rnnTrialCount = 1)
    {
      int offset = Math.Min((int)(Member.TrainingOffsetPct * Versace.TrainingInput.ColumnCount), Versace.TrainingInput.ColumnCount - 1);
      int size = Math.Max(1, Math.Min((int)(Member.TrainingSizePct * Versace.TrainingInput.ColumnCount), Versace.TrainingInput.ColumnCount - offset));
      var outputs = Versace.TrainingOutput.SubVector(offset, size);
      var inputs = Versace.TrainingInput.Columns().Skip(offset).Take(size).ToList(); // TODO: don't call Columns
      inputs = Preprocess(inputs, true);

      if (Member.NetworkType == NetworkType.RNN)
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
        if (rnnTrialCount > 1)
          RNN.TrainSCGMulti((RNN)rnn, Member.ElmanTrainingEpochs, Versace.MatrixFromColumns(inputs), outputs, rnnTrialCount);
        else
          RNN.TrainSCG((RNN)rnn, Member.ElmanTrainingEpochs, Versace.MatrixFromColumns(inputs), outputs);
        Network = rnn;
      }
      else
      {
        Network = RBFNet.Train(Versace.MatrixFromColumns(inputs), (Vector)outputs, Member.RbfNetTolerance, Member.RbfGaussianSpread);
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
      var eExpert = new XElement("Expert",
        new XAttribute("PreprocessingType", PreprocessingType),
        Network.ToXml());
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
      var expert = new Expert(member, (PreprocessingType)Enum.Parse(typeof(PreprocessingType), eExpert.Attribute("PreprocessingType").Value)) {
        Network = eNetwork.Attribute("Type").Value == NetworkType.RNN.ToString()
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

  public static class VersaceBacktest
  {
    public static BacktestReport Backtest(PredictionType predictionType, IPredictor predictor, Account account, Matrix preInputs, Matrix inputs, DataSeries<Bar> bars)
    {
      double maxAccountLossPct = 0.025;

      // run through the preTesting values so the RNNs can build up state
      predictor.Reset();
      foreach (var input in preInputs.Columns())
        predictor.Predict(input);

      List<SignalValue> signal = null;
      if (predictionType == PredictionType.NextClose)
        signal = MakeSignalNextClose(predictor, inputs.Columns(), bars, maxAccountLossPct);
      BacktestReport report = PlaybackSignal(signal, bars, account);
      return report;
    }

    static List<SignalValue> MakeSignalNextClose(IPredictor predictor, List<Vector> inputs, DataSeries<Bar> bars, double maxAccountLossPct)
    {
      var signal = new List<SignalValue>();
      DataSeries<Value> buySell = inputs.Select(x => (double)Math.Sign(predictor.Predict(x))).ToDataSeries(bars);
      int riskATRPeriod = 7;
      double riskScale = 1.2;
      double riskRatio = 0.65;
      DataSeries<Value> perShareRisk =
        bars.OpeningWickHeight().EMA(riskATRPeriod).ZipElements<Value, Value>(
        bars.ATR(riskATRPeriod), (w, a, v) =>
          riskScale * (riskRatio * w[0] + (1 - riskRatio) * a[0]));

      for (int i = 0; i < bars.Length; i++)
      {
        var bias = buySell[i].Val > 0 ? SignalBias.Buy : SignalBias.Sell;
        var sizePct = maxAccountLossPct / perShareRisk[i];
        double absoluteStop;
        if (bias == SignalBias.Buy)
          absoluteStop = bars[i].Open - perShareRisk[0];
        else
          absoluteStop = bars[i].Open + perShareRisk[0];
        signal.Add(new SignalValue(bars[i].Timestamp, bias, SignalTimeOfDay.Close, sizePct, absoluteStop, null));
      }
      return signal;
    }

    private static BacktestReport PlaybackSignal(List<SignalValue> signal, DataSeries<Bar> bars, Account account)
    {
      throw new NotImplementedException();
    }
  }
}
