using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Quqe;
using PCW;
using System;
using StockCharts;
using System.Windows.Media;
using System.IO;
using System.Diagnostics;
using System.Windows.Controls;
using QuqeViz.Properties;
using DotNumerics.ODE;
using MathNet.Numerics.LinearAlgebra.Double;
using Vector = MathNet.Numerics.LinearAlgebra.Double.Vector;
using Matrix = MathNet.Numerics.LinearAlgebra.Double.Matrix;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime;

namespace QuqeViz
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
      Initialize();
      Update();
      var btw = new BacktestWindow();
      btw.Show();
      this.Loaded += delegate { btw.Activate(); };
    }

    public void DoBacktest(string symbol, string strategyName, double initialValue, int marginFactor, bool isValidation)
    {
      var bars = Data.Get(symbol);
      var strat = StrategyOptimizerReport.CreateStrategy(strategyName);
      var signal = strat.MakeSignal(bars.From(ValidationStartBox.Text).To(ValidationEndBox.Text));
      var backtestReport = Strategy.BacktestSignal(bars, signal,
        new Account {
          Equity = initialValue,
          MarginFactor = marginFactor,
          Padding = 80,
          //IgnoreGains = true
        }, 0, null);
      Trace.WriteLine(string.Format("Training  :  {0}  -  {1}", TrainingStartBox.Text, TrainingEndBox.Text));
      bool validationWarning = DateTime.Parse(ValidationStartBox.Text) <= DateTime.Parse(TrainingEndBox.Text);
      Trace.WriteLine(string.Format("Validation:  {0}  -  {1}{2}",
        ValidationStartBox.Text, ValidationEndBox.Text, validationWarning ? " !!!!!" : ""));
      Trace.WriteLine(backtestReport.ToString());
      var bs = bars.From(signal.First().Timestamp).To(signal.Last().Timestamp);
      ShowBacktestChart(bs, backtestReport.Trades, signal, initialValue, marginFactor, isValidation, strategyName, strat.SParams);

      int wrong = 0;
      int total = 0;
      DataSeries.Walk(signal, bs, pos => {
        if (pos == 0)
          return;
        if (signal[0].Bias != signal[1].Bias)
        {
          if (bs[0].IsGreen != (signal[0].Bias == SignalBias.Buy))
            wrong++;
          total++;
        }
      });
    }

    static void ShowBacktestChart(DataSeries<Bar> bars, List<TradeRecord> trades, DataSeries<SignalValue> signal,
      double initialValue, int marginFactor, bool isValidation, string strategyName, IEnumerable<StrategyParameter> sParams)
    {
      var profitPerTrade = trades.ToDataSeries(t => t.Profit * 100.0);
      var accountValue = trades.ToDataSeries(t => t.AccountValueAfterTrade);
      var otpdTrades = OTPDStrategy.GetTrades(true, true, true, true, 1000000);

      var w = new ChartWindow();
      w.Title = bars.Symbol + " : " + (!isValidation ? "Training" : "Validation");
      var g1 = w.Chart.AddGraph();
      g1.Title = bars.Symbol;
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      g1.Plots.Add(new Plot {
        Title = "PCW Stops",
        DataSeries = trades.ToDataSeries(t => t.StopLimit),
        Type = PlotType.Dash,
        Color = Brushes.Blue
      });
      g1.AddTrades(trades);

      var g2 = w.Chart.AddGraph();
      g2.Plots.Add(new Plot {
        Title = "Profit per trade",
        DataSeries = profitPerTrade,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });

      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = string.Format("Initial Value: ${0:N0}   Margin: {1}", initialValue, marginFactor == 1 ? "none" : marginFactor + "x"),
        DataSeries = accountValue,
        Type = PlotType.ValueLine,
        Color = Brushes.Blue,
        LineThickness = 2
      });

      w.Show();
    }

    void Initialize()
    {
      Action<string, ComboBox> deserialize = (n, cb) => {
        var v = (string)Settings.Default[n];
        if (v == "")
          return;
        cb.Items.Clear();
        cb.Items.AddRange(v.Split(','));
        cb.SelectedItem = cb.Items[0];
      };

      deserialize("RecentTrainingStartDates", TrainingStartBox);
      deserialize("RecentTrainingEndDates", TrainingEndBox);
      deserialize("RecentValidationStartDates", ValidationStartBox);
      deserialize("RecentValidationEndDates", ValidationEndBox);
    }

    void Update()
    {
      var selected = StrategiesBox.SelectedItem;
      StrategiesBox.Items.Clear();
      if (!Directory.Exists(StrategyOptimizerReport.StrategyDir))
        return;
      foreach (var fn in Directory.EnumerateFiles(StrategyOptimizerReport.StrategyDir)
        .OrderByDescending(x => new FileInfo(Path.Combine(StrategyOptimizerReport.StrategyDir, x)).LastWriteTime))
        StrategiesBox.Items.Add(Path.GetFileNameWithoutExtension(fn));
      StrategiesBox.SelectedItem = StrategiesBox.Items[0];

      Action<ComboBox> add = cb => {
        if (!cb.Items.Contains(cb.Text))
          cb.Items.Add(cb.Text);
      };

      add(TrainingStartBox);
      add(TrainingEndBox);
      add(ValidationStartBox);
      add(ValidationEndBox);

      Action<string, ComboBox> serialize = (n, cb) => {
        if (cb.Text == "")
          return;
        Settings.Default[n] = List.Create(cb.Text).Concat(cb.Items.Cast<string>().Except(List.Create("", cb.Text))).Distinct().Join(",");
      };

      serialize("RecentTrainingStartDates", TrainingStartBox);
      serialize("RecentTrainingEndDates", TrainingEndBox);
      serialize("RecentValidationStartDates", ValidationStartBox);
      serialize("RecentValidationEndDates", ValidationEndBox);
      Settings.Default.Save();
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
      DoBacktest(SymbolBox.Text, (string)StrategiesBox.SelectedItem,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), true);
      Update();
    }

    private void ParallelCheckBox_Checked(object sender, RoutedEventArgs e)
    {
      Optimizer.ParallelizeStrategyOptimization = ParallelCheckBox.IsChecked.Value;
    }

    private void GetVersaceDataButton_Click(object sender, RoutedEventArgs e)
    {
      Versace.GetData("DIA", DateTime.Parse("11/11/2001"), DateTime.Now.Date);
    }

    private void DIAButton_Click(object sender, RoutedEventArgs e)
    {
      var dia = Versace.GetCleanSeries("DIA", Versace.GetTickers("DIA")).First(s => s.Symbol == "DIA");
      var w = new ChartWindow();
      var g1 = w.Chart.AddGraph();
      g1.Plots.Add(new Plot {
        Title = "DIA",
        DataSeries = dia,
        Type = PlotType.Candlestick
      });
      var g2 = w.Chart.AddGraph();
      w.Show();
    }

    private void VersaceEvolveButton_Click(object sender, RoutedEventArgs e)
    {
      var ch = new EqPlotWindow();
      ch.Show();
      var mainSync = SyncContext.Current;
      Action<List<double>> updateHistoryWindow = history => {
        mainSync.Post(() => {
          ch.EqPlot.Clear(Colors.White);
          ch.EqPlot.Bounds = new Rect(0, history.Min(), history.Count, history.Max() - history.Min());
          ch.EqPlot.DrawLine(List.Repeat(history.Count, i => new Point(i, history[i])), Colors.Blue);
        });
      };
      Thread t = new Thread(() => Versace.Evolve(updateHistoryWindow));
      //var prevResult = VersaceResult.Load("VersaceResults/VersaceResult-20121015-065326.xml");
      //Thread t = new Thread(() => Versace.Anneal(prevResult.BestMixture, updateHistoryWindow));
      t.Start();
    }

    private void BacktestVersaceButton_Click(object sender, RoutedEventArgs e)
    {
      var fn = Directory.EnumerateFiles("VersaceResults").OrderByDescending(x => new FileInfo(x).LastWriteTime).First();
      //var vr = VersaceResult.Load(fn);
      var vr = VersaceResult.Load("VersaceResults/VersaceResult-20121015-065326.xml");
      var m = vr.BestMixture;
      m.Dump();

      var ch = new EqPlotWindow();
      ch.EqPlot.Bounds = new Rect(0, vr.FitnessHistory.Min(), vr.FitnessHistory.Count, vr.FitnessHistory.Max() - vr.FitnessHistory.Min());
      ch.EqPlot.DrawLine(List.Repeat(vr.FitnessHistory.Count, i => new Point(i, vr.FitnessHistory[i])), Colors.Blue);
      ch.Show();

      //m.Reset();
      //var output = Versace.TestingInput.Columns().Select(x => (double)Math.Sign(m.Predict(x))).ToList();
      //var inputSeries = new DataSeries<Bar>(Versace.DIA.Symbol, Versace.DIA.Skip(Versace.TrainingOutput.Count));
      //var idealSignal = new DataSeries<Value>("", Versace.TestingOutput.ToDataSeries(inputSeries));
      //var actualSignal = new DataSeries<Value>("", output.ToDataSeries(inputSeries));
      //var diff = idealSignal.ZipElements<Value, Value>(actualSignal, (i, a, _) => i[0].Val == a[0].Val ? 1 : -1);
      //Trace.WriteLine(string.Format("Accuracy: {0:N1}%", (double)diff.Count(x => x.Val == 1) / diff.Length * 100));

      //var w = new ChartWindow();
      //var g1 = w.Chart.AddGraph();
      //g1.Plots.Add(new Plot {
      //  Title = "DIA",
      //  DataSeries = inputSeries,
      //  Type = PlotType.Candlestick
      //});
      //var g2 = w.Chart.AddGraph();
      //g2.Plots.Add(new Plot {
      //  Title = "IdealSignal",
      //  DataSeries = idealSignal,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Blue
      //});
      //var g3 = w.Chart.AddGraph();
      //g3.Plots.Add(new Plot {
      //  Title = "ActualSignal",
      //  DataSeries = actualSignal,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Blue
      //});
      //var g4 = w.Chart.AddGraph();
      //g4.Plots.Add(new Plot {
      //  Title = "Diff",
      //  DataSeries = diff,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Blue
      //});
      //w.Show();
    }

    private void GrillButton_Click(object sender, RoutedEventArgs e)
    {
      var ch = new EqPlotWindow();
      ch.Show();
      var mainSync = SyncContext.Current;
      Action<List<double>> updateHistoryWindow = history => {
        mainSync.Post(() => {
          ch.EqPlot.Clear(Colors.White);
          ch.EqPlot.Bounds = new Rect(0, history.Min(), history.Count, history.Max() - history.Min());
          ch.EqPlot.DrawLine(List.Repeat(history.Count, i => new Point(i, history[i])), Colors.Blue);
        });
      };


      var vr = VersaceResult.Load("VersaceResults/VersaceResult-20121015-065326.xml");
      Thread t = new Thread(() => {
        RNN.ShouldTrace = false;
        RBFNet.ShouldTrace = false;
        Trace.WriteLine("Original fitness: " + vr.BestMixture.Fitness);
        VMixture m = vr.BestMixture;
        var fitnessHistory = new List<double> { m.Fitness };
        updateHistoryWindow(fitnessHistory);
        for (int i = 0; i < 100; i++)
        {
          Parallel.ForEach(m.Members, member => {
            member.RefreshExpert();
            member.Expert.TrainEx(rnnTrialCount: 4);
          });
          m.ComputeFitness();
          Trace.WriteLine("[" + i + "] Fitness: " + m.Fitness);
          fitnessHistory.Add(m.Fitness);
          updateHistoryWindow(fitnessHistory);
        }
      });
      t.Start();
    }
  }
}