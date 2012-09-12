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
      if (strat is DTStrategy)
        EvalAndDumpDTStrategy((DTStrategy)strat);
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

    public void EvalAndDumpDTStrategy(DTStrategy strat)
    {
      var bars = Data.Get(SymbolBox.Text);
      var dt = strat.MakeDt(DateTime.Parse(TrainingStartBox.Text), bars.To(TrainingEndBox.Text));
      var validationSet = strat.MakeDtExamples(DateTime.Parse(ValidationStartBox.Text), bars.To(ValidationEndBox.Text)).ToList();
      Optimizer.DTQuality(dt, validationSet, true);
    }

    //public static void DoGenomelessBacktest(string symbol, string startDate, string endDate, Strategy strat, double initialValue, int marginFactor, bool isValidation)
    //{
    //  var bars = Data.Get(symbol).From(startDate).To(endDate);
    //  strat.ApplyToBars(bars);
    //  var backtestReport = strat.Backtest(null, new Account { Equity = initialValue, MarginFactor = marginFactor, Padding = 50 });
    //  Trace.WriteLine(backtestReport.ToString());
    //  Strategy.WriteTrades(backtestReport.Trades, DateTime.Now, "no-genome");
    //  ShowBacktestChart(bars, backtestReport.Trades, initialValue, marginFactor, isValidation, strat.Name, null);
    //}

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
      g1.Plots.Add(new Plot {
        Title = "OTPD Stops",
        DataSeries = otpdTrades.ToDataSeries(t => t.StopLimit),
        Type = PlotType.Dash,
        Color = Brushes.Goldenrod
      });
      //g1.Plots.Add(new Plot {
      //  DataSeries = bars.Closes().LinReg(2, 1).Delay(1),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Blue
      //});
      //g1.Plots.Add(new Plot {
      //  DataSeries = bars.Closes().LinReg(7, 1).Delay(1),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Red
      //});
      //g1.Plots.Add(new Plot {
      //  DataSeries = signal.MapElements<Value>((s, v) => s[0].Stop),
      //  Type = PlotType.Dash,
      //  Color = Brushes.Blue
      //});
      g1.AddTrades(trades);

      //var g4 = w.Chart.AddGraph();
      //g4.Plots.Add(new Plot {
      //  Title = "RSquared(10)",
      //  DataSeries = bars.Closes().RSquared(10).Delay(1),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Red
      //});
      //g4.Plots.Add(new Plot {
      //  DataSeries = bars.ConstantLine(0.75),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.GreenYellow
      //});

      //var g5 = w.Chart.AddGraph();
      //g5.Plots.Add(new Plot {
      //  Title = "LinRegSlope(4)",
      //  DataSeries = bars.Closes().LinRegSlope(4).Delay(1),
      //  Type = PlotType.Bar,
      //  Color = Brushes.Gray
      //});

      //var g2 = w.Chart.AddGraph();
      //g2.Plots.Add(new Plot {
      //  Title = "Profit per trade",
      //  DataSeries = profitPerTrade,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Blue
      //});

      var simpleSignal = signal.ToSimpleSignal();
      var simpleOtpdSignal = otpdTrades.ToDataSeries(t => t.PositionDirection == PositionDirection.Long ? 1 : -1)
        .From(bars.First().Timestamp);
      var signalDiffElements = new List<Value>();
      DataSeries.Walk(bars, simpleSignal, simpleOtpdSignal, pos => {
        signalDiffElements.Add(new Value(bars[0].Timestamp,
          Math.Sign(simpleSignal[0]) == Math.Sign(simpleOtpdSignal[0]) ? 0 :
          (simpleSignal[0] >= 0) == bars[0].IsGreen ? 1 :
          -1));
      });
      var signalDiff = new DataSeries<Value>(bars.Symbol, signalDiffElements);

      Trace.WriteLine(string.Format("When different, I'm right {0} times, they're right {1} times",
        signalDiff.Count(x => x > 0), signalDiff.Count(x => x < 0)));

      var y = otpdTrades.ToDataSeries(t => t.IsWin ? -1 : Math.Abs(t.Exit - t.Entry)).From(bars.First().Timestamp)
        .ZipElements<Bar, Value>(bars, (l, b, v) => {
          if (l[0] == -1)
            return 0;
          if (b[0].WaxHeight() - l[0] > 0.03)
            return 2;
          else
            return 1;
        });

      //var g9 = w.Chart.AddGraph();
      //g9.Plots.Add(new Plot {
      //  DataSeries = y,
      //  Type = PlotType.Bar,
      //  Color = Brushes.DeepPink
      //});

      Trace.WriteLine(string.Format("{0}% of OTPD losses are stopped", (double)y.Count(x => x == 2) / y.Count(x => x > 0)));

      var g2b = w.Chart.AddGraph();
      g2b.Plots.Add(new Plot {
        Title = "OTPD",
        DataSeries = simpleOtpdSignal,
        Type = PlotType.Bar,
        Color = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0))
      });
      g2b.Plots.Add(new Plot {
        Title = "Signal",
        DataSeries = simpleSignal,
        Type = PlotType.Bar,
        Color = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255))
      });

      var g4 = w.Chart.AddGraph();
      g4.Plots.Add(new Plot {
        Title = "Pos = I was right, Neg = OTPD was right",
        DataSeries = signalDiff,
        Type = PlotType.Bar,
        Color = Brushes.DarkGray
      });

      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = string.Format("OTPD"),
        DataSeries = otpdTrades.ToDataSeries(t => t.AccountValueAfterTrade),
        Type = PlotType.ValueLine,
        Color = Brushes.Red,
        LineThickness = 2
      });
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

    //private void BacktestButton_Click(object sender, RoutedEventArgs e)
    //{
    //  DoBacktest(SymbolBox.Text, TrainingStartBox.Text, TrainingEndBox.Text, (string)StrategiesBox.SelectedItem,
    //    double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
    //  Update();
    //}

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
      DoBacktest(SymbolBox.Text, (string)StrategiesBox.SelectedItem,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), true);
      Update();
    }

    void ContributionsButton_Click(object sender, RoutedEventArgs e)
    {
      //PrintContributions(BuySellBox.Text);
    }

    void PrintContributions(string reportName)
    {
      var report = StrategyOptimizerReport.Load(reportName);
      var strat = Strategy.Make(report.StrategyName, report.StrategyParams);
      var c = strat.CalculateContributions(Genome.Load(report.GenomeName));

      Trace.WriteLine("== Contributions ==");
      foreach (var kv in c)
        Trace.WriteLine(kv.Key + ": " + (kv.Value * 100).ToString("N1") + " %");
      Trace.WriteLine("===================");
    }

    private void TQQQButton_Click(object sender, RoutedEventArgs e)
    {
      var tqqq = Data.Get("TQQQ");
      var window = new ChartWindow();
      var chart = window.Chart;
      window.Show();
      chart.ScrollToEnd();

      //DateTime currentEnd = tqqq.First().Timestamp.AddMonths(24);
      DateTime currentEnd = tqqq.Last().Timestamp;

      Action drawIt = () => {
        var bars = tqqq.To(currentEnd);
        chart.ClearGraphs();
        var g = chart.AddGraph();
        g.Title = "TQQQ";
        g.Plots.Add(new Plot {
          DataSeries = bars,
          Type = PlotType.Candlestick
        });
        g.Plots.Add(new Plot {
          DataSeries = bars.DonchianMin(10).Delay(1),
          Type = PlotType.ValueLine,
          Color = Brushes.Goldenrod
        });
        g.Plots.Add(new Plot {
          DataSeries = bars.DonchianMax(10).Delay(1),
          Type = PlotType.ValueLine,
          Color = Brushes.Goldenrod
        });

        var g5 = chart.AddGraph();
        g5.Plots.Add(new Plot {
          Title = "ReversalIndex",
          DataSeries = bars.ReversalIndex(10),
          Type = PlotType.ValueLine,
          Color = Brushes.Blue
        });

        var g6 = chart.AddGraph();
        g6.Plots.Add(new Plot {
          Title = "AntiTrendIndex",
          DataSeries = bars.AntiTrendIndex(6, 12).Delay(1),
          Type = PlotType.ValueLine,
          Color = Brushes.DarkRed
        });

        var g7 = chart.AddGraph();
        g7.Plots.Add(new Plot {
          Title = "LinRegSlope",
          DataSeries = bars.Closes().LinRegSlope(2).Delay(1),
          Type = PlotType.ValueLine,
          Color = Brushes.Green
        });

        var wickStops = bars.WickStops(9, 3, 8);

        var g2 = chart.AddGraph();
        g2.Plots.Add(new Plot {
          Title = "ATR",
          DataSeries = bars.ATR(16),
          Type = PlotType.ValueLine,
          Color = Brushes.Blue
        });
        g2.Plots.Add(new Plot {
          Title = "AWH",
          DataSeries = bars.OpeningWickHeight().EMA(16),
          Type = PlotType.ValueLine,
          Color = Brushes.Black
        });
        g2.Plots.Add(new Plot {
          DataSeries = bars.OpeningWickHeight().EMA(16).ZipElements<Value, Value>(bars.ATR(16), (w, a, v) => (2 * w[0] + 0.5 * a[0]) / 2),
          Type = PlotType.ValueLine,
          Color = Brushes.OrangeRed
        });
        //g2.Plots.Add(new Plot {
        //  DataSeries = bars.OpeningWickHeight().EMA(16).MapElements<Value>((s, v) => s[0] * 4.5),
        //  Type = PlotType.ValueLine,
        //  Color = Brushes.OrangeRed
        //});
        //g2.Plots.Add(new Plot {
        //  Title = "OpeningWick-modified ATR",
        //  DataSeries = bars.ATR(16).ZipElements<Value, Value>(bars.OpeningWickHeight().EMA(16), (a, w, v) => (a[0] + 4.5 * w[0]) / 2),
        //  Type = PlotType.ValueLine,
        //  Color = Brushes.Black
        //});

        var g3 = chart.AddGraph();
        g3.Plots.Add(new Plot {
          Title = "Wick Stops",
          DataSeries = wickStops,
          Type = PlotType.Bar,
          Color = Brushes.OrangeRed
        });
        g3.Plots.Add(new Plot {
          DataSeries = bars.OpeningWickHeight().EMA(16),
          Type = PlotType.ValueLine,
          Color = Brushes.Black
        });

        chart.ScrollToEnd();
      };

      chart.NavigatePrev += () => {
        do
        {
          currentEnd = currentEnd.AddDays(-1);
        } while (!tqqq.Any(b => b.Timestamp == currentEnd));
        drawIt();
      };

      chart.NavigateNext += () => {
        do
        {
          currentEnd = currentEnd.AddDays(1);
        } while (!tqqq.Any(b => b.Timestamp == currentEnd));
        drawIt();
      };

      drawIt();
    }

    private void ParallelCheckBox_Checked(object sender, RoutedEventArgs e)
    {
      Optimizer.ParallelizeStrategyOptimization = ParallelCheckBox.IsChecked.Value;
    }

    private void OptimizeDTCandlesButton_Click(object sender, RoutedEventArgs e)
    {
      var oParams = List.Create(
        //new OptimizerParameter("MinMajority", 0.50, 0.60, 0.01),
        new OptimizerParameter("SmallMax", 0.0, 2.00, 0.01),
        new OptimizerParameter("MediumMax", 0.0, 4.00, 0.01),
        new OptimizerParameter("GapPadding", 0.0, 1.00, 0.01),
        new OptimizerParameter("SuperGapPadding", 0.0, 1.00, 0.01),
        new OptimizerParameter("EnableEma", 1, 1, 1),
        new OptimizerParameter("FastEmaPeriod", 3, 8, 1),
        new OptimizerParameter("SlowEmaPeriod", 9, 20, 1),
        new OptimizerParameter("EnableMomentum", 1, 1, 1),
        new OptimizerParameter("MomentumPeriod", 3, 13, 1),
        new OptimizerParameter("EnableRSquared", 1, 1, 1),
        new OptimizerParameter("RSquaredPeriod", 3, 13, 1),
        new OptimizerParameter("EnableLinRegSlope", 1, 1, 1),
        new OptimizerParameter("LinRegSlopePeriod", 3, 13, 1)
        );

      var periodParams = oParams.Where(x => x.Name.EndsWith("Period"));
      int lookback = !periodParams.Any() ? 2 : (int)(periodParams.Max(x => (int)x.High) * 7.0 / 5.0 + 2);

      //Optimizer.OptimizeDecisionTree("DTCandles", oParams, 1000,
      //  DateTime.Parse(TrainingStartBox.Text), Data.Get(SymbolBox.Text).To(TrainingEndBox.Text),
      //  TimeSpan.FromDays(lookback), sParams => 0, DTCandlesStrategy.MakeExamples);

      Optimizer.OptimizeDecisionTree("DTCandles", oParams, 30,
        DateTime.Parse(TrainingStartBox.Text), Data.Get(SymbolBox.Text).To(TrainingEndBox.Text),
        TimeSpan.FromDays(45),
        TimeSpan.FromDays(lookback), sParams => 0, DTCandlesStrategy.MakeExamples);

      Update();
    }

    private void OptimizeDTBarSumButton_Click(object sender, RoutedEventArgs e)
    {
      var oParams = List.Create(
        new OptimizerParameter("GapPadding", 0.0, 1.00, 0.01),
        new OptimizerParameter("SuperGapPadding", 0.0, 1.00, 0.01),
        new OptimizerParameter("BarSumPeriod", 3, 20, 1),
        new OptimizerParameter("BarSumNormalizingPeriod", 3, 40, 1),
        new OptimizerParameter("BarSumSmoothing", 1, 5, 1),
        new OptimizerParameter("BarSumThresh", 0.0, 1.0, 0.01),
        new OptimizerParameter("EmaPeriod", 3, 20, 1),
        new OptimizerParameter("EmaThresh", 0.0, 4, 0.1),
        new OptimizerParameter("LinRegPeriod", 3, 15, 1),
        new OptimizerParameter("LinRegForecast", 0, 4, 1)
        );

      var periodParams = oParams.Where(x => x.Name.EndsWith("Period"));
      int lookback = !periodParams.Any() ? 2 : (int)(periodParams.Max(x => (int)x.High) * 7.0 / 5.0 + 2);

      //Optimizer.OptimizeDecisionTree("DTBarSum", oParams, 10000,
      //  DateTime.Parse(TrainingStartBox.Text), Data.Get(SymbolBox.Text).To(TrainingEndBox.Text),
      //  TimeSpan.FromDays(lookback), sParams => 0, DTBarSumStrategy.MakeExamples);

      Optimizer.OptimizeDecisionTree("DTBarSum", oParams, 25000,
        DateTime.Parse(TrainingStartBox.Text), Data.Get(SymbolBox.Text).To(TrainingEndBox.Text),
        TimeSpan.FromDays(45),
        TimeSpan.FromDays(lookback), sParams => 0, DTBarSumStrategy.MakeExamples);

      Update();
    }

    private void OptimizeTrending1Button_Click(object sender, RoutedEventArgs e)
    {
      var oParams = List.Create(
        new OptimizerParameter("WO", 0, 0, 1),
        new OptimizerParameter("WL", 0, 0, 1),
        new OptimizerParameter("WH", 0, 0, 1),
        new OptimizerParameter("WC", 1, 1, 1),
        new OptimizerParameter("FastRegPeriod", 2, 2, 1),
        new OptimizerParameter("SlowRegPeriod", 7, 7, 1),

        // long trends
        new OptimizerParameter("RSquaredPeriod", 10, 10, 1),
        new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
        new OptimizerParameter("LinRegSlopePeriod", 4, 4, 1),

        //// stops
        //new OptimizerParameter("WickStopPeriod", 2, 12, 1),
        //new OptimizerParameter("WickStopCutoff", 1, 6, 1),
        //new OptimizerParameter("WickStopSmoothing", 0, 8, 1)

        // risk
        new OptimizerParameter("RiskATRPeriod", 7, 7, 1),
        new OptimizerParameter("MaxAccountLossPct", 0.035, 0.035, 0.005),
        new OptimizerParameter("M", 1.20, 1.20, 0.05),
        new OptimizerParameter("S", 0.65, 0.65, 0.10)
        //new OptimizerParameter("M", 1.00, 2.00, 0.05),
        //new OptimizerParameter("S", 0.25, 1.00, 0.10)
        );

      var symbol = SymbolBox.Text;
      var start = TrainingStartBox.Text;
      var end = TrainingEndBox.Text;

      //Func<IEnumerable<StrategyParameter>, double> calcFitness = sParams => {
      //  var bars = Data.Get(symbol).From(start).To(end);
      //  var strat = new Trending1Strategy(sParams);
      //  var signal = strat.MakeSignal(default(DateTime), null, bars.First().Timestamp, bars);
      //  return bars.From(signal.First().Timestamp).SignalAccuracyPercent(signal);
      //};

      Func<IEnumerable<StrategyParameter>, double> calcFitness = sParams => {
        //if (sParams.Get<int>("WickStopCutoff") >= sParams.Get<int>("WickStopPeriod"))
        //  return 0;
        var bars = Data.Get(symbol).From(start).To(end);
        var strat = new Trending1Strategy(sParams);
        var signal = strat.MakeSignal(bars);
        var bt = Strategy.BacktestSignal(bars, signal,
          new Account { Equity = 50000, MarginFactor = 12, Padding = 120 }, 0, null);
        return bt.TotalReturn;
      };

      //Func<IEnumerable<StrategyParameter>, double> calcFitness = sParams => {
      //  var bars = Data.Get(symbol).From(start).To(end);
      //  TimeSpan sampleSize = TimeSpan.FromDays(100);
      //  double fitnessSum = 0;
      //  int windowCount = 0;
      //  for (DateTime windowStart = bars.First().Timestamp;
      //    windowStart.Add(sampleSize) < bars.Last().Timestamp;
      //    windowStart = windowStart.Add(sampleSize))
      //  {
      //    var strat = new Trending1Strategy(sParams);
      //    var bs = bars.From(windowStart).To(windowStart.Add(sampleSize).AddDays(-1));
      //    var signal = strat.MakeSignal(bs);
      //    var bt = Strategy.BacktestSignal(bars, signal,
      //      new Account { Equity = 15000, MarginFactor = 1, Padding = 20 }, 0, null);
      //    fitnessSum += bt.TotalReturn;
      //    windowCount++;
      //  }
      //  return fitnessSum / windowCount;
      //};

      //Func<IEnumerable<StrategyParameter>, double> calcFitness = sParams => {
      //  var bars = Data.Get(symbol).From(start).To(end);
      //  TimeSpan sampleSize = TimeSpan.FromDays(100);
      //  double fitnessSum = 0;
      //  int windowCount = 0;
      //  for (DateTime windowStart = bars.First().Timestamp;
      //    windowStart.Add(sampleSize) < bars.Last().Timestamp;
      //    windowStart = windowStart.Add(sampleSize))
      //  {
      //    var strat = new Trending1Strategy(sParams);
      //    var bs = bars.From(windowStart).To(windowStart.Add(sampleSize).AddDays(-1));
      //    var signal = strat.MakeSignal(default(DateTime), null, bs.First().Timestamp, bs);
      //    fitnessSum += bs.SignalAccuracyPercent(signal);
      //    windowCount++;
      //  }
      //  return fitnessSum / windowCount;
      //};

      var reports = Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        return new StrategyOptimizerReport {
          StrategyName = "Trending1",
          StrategyParams = sParams,
          Fitness = calcFitness(sParams)
        };
      });

      //var best = Optimizer.Anneal(oParams, sParams => -calcFitness(sParams), 1000, partialCool: true);
      //var reports = List.Create(new StrategyOptimizerReport {
      //  StrategyName = "Trending1",
      //  StrategyParams = best.Params.ToList(),
      //  Fitness = -best.Cost
      //});

      Strategy.PrintStrategyOptimizerReports(reports);

      Update();
    }

    private void ShowOtpdTradesButton_Click(object sender, RoutedEventArgs e)
    {
      var trades = OTPDStrategy.GetTrades(true, true, false, true, 1000000);
      var profit = trades.ToDataSeries(t => t.PercentProfit * 100);
      var signal = trades.ToDataSeries(t => t.PositionDirection == PositionDirection.Long ? 1 : -1);

      Trace.WriteLine("Accuracy: " + ((double)trades.Count(t => t.IsWin) / trades.Count));

      var w = new ChartWindow();
      var g = w.Chart.AddGraph();
      g.Plots.Add(new Plot {
        Title = "QQQ",
        DataSeries = Data.Get("QQQ").From("06/07/2010").To("07/18/2012"),
        Type = PlotType.Candlestick
      });
      g.Plots.Add(new Plot {
        DataSeries = trades.ToDataSeries(t => t.StopLimit > 0 ? t.StopLimit : t.Exit),
        Type = PlotType.Dash,
        Color = Brushes.Blue
      });
      g.AddTrades(trades);
      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = "Profit % per trade",
        DataSeries = profit,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });
      //var g2 = w.Chart.AddGraph();
      //g2.Plots.Add(new Plot {
      //  Title = "Signal",
      //  DataSeries = signal,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Purple
      //});
      var g4 = w.Chart.AddGraph();
      g4.Plots.Add(new Plot {
        DataSeries = trades.ToDataSeries(t => t.AccountValueAfterTrade),
        Type = PlotType.ValueLine,
        Color = Brushes.Green,
        LineThickness = 2
      });

      w.Show();
    }

    private void FooButton_Click(object sender, RoutedEventArgs e)
    {
      var w = new EqPlotWindow();
      //Bounds = new Rect(0, 0, 1.4, 1.4);
      //Func<double, double, double> f = (x, y) => Math.Pow(x - 1, 4) + Math.Pow(y - 1, 4);
      w.EqPlot.Bounds = new Rect(-5, -5, 10, 10);
      Func<double, double, double> f = (x, y) =>
        20 + (Math.Pow(x, 2) - 10 * Math.Cos(2 * Math.PI * x) + Math.Pow(y, 2) - 10 * Math.Cos(2 * Math.PI * y));
      //var result = BCO.Optimize(new double[] { 0, 0 }, x => f(x[0], x[1]), 0.000026, 1530, 3650, 1);
      //var result = BCO.Optimize(new double[] { -5, -5 }, x => f(x[0], x[1]), 0.0005, 10, 790, 1);
      //var result = BCO.Optimize(new double[] { 0, 0 }, x => f(x[0], x[1]), 0.000001);
      var result1 = BCO.Optimize(new double[] { -5, -5 }, x => f(x[0], x[1]), 30000, Math.Pow(10, -4), 12, 10);
      var result2 = BCO.OptimizeWithParameterAdaptation(result1.MinimumLocation, x => f(x[0], x[1]), 3000, 7, 10);
      var result3 = BCO.Optimize(result2.MinimumLocation, x => f(x[0], x[1]), 3000, Math.Pow(10, -11), 6, 10);
      w.EqPlot.DrawSurface(f, DrawMode.Gradient);
      w.EqPlot.DrawLine(result3.Path.Select(x => new Point(x[0], x[1])), Colors.Red);
      w.Show();
    }

    private void BarButton_Click(object sender, RoutedEventArgs e)
    {
      var w = new EqPlotWindow();
      w.EqPlot.Bounds = new Rect(-20, 0, 40, 1.75);
      var originalIdeal = new List<Point>();
      for (double x = -20; x <= 20; x += 0.2)
        originalIdeal.Add(new Point(x, 0.25 + Math.Sin(1.5 * x) / (1.5 * x))); // sinc function
      var noisy = originalIdeal.Select(p => new Point(p.X, p.Y + BCO.RandomGaussian(0, 0.02))).ToList();
      var noisyValues = noisy.Select(p => p.Y).ToList();
      var noisyValues2 = noisyValues.Skip(1).ToList();
      var normalizedNoisyValues = noisyValues.Take(noisyValues.Count - 1).Zip(noisyValues.Skip(1), (v1, v0) => (v0 - v1) / v0).ToList();
      var ideal = originalIdeal.Skip(1).ToList();
      Debug.Assert(ideal.Count == normalizedNoisyValues.Count);

      int numInputs = 3;
      Func<ElmanNet> makeNet = () => new ElmanNet(numInputs, List.Create(20), 1);
      var net = makeNet();
      var report = BCO.Optimize(new double[net.WeightVectorLength], weights => {
        net.SetWeightVector(weights.ToArray());
        double absoluteErrorSum = 0;
        for (int i = numInputs; i < ideal.Count; i++)
        {
          var output = net.Propagate(new double[] { normalizedNoisyValues[i - 1], normalizedNoisyValues[i - 2], normalizedNoisyValues[i - 3] });
          absoluteErrorSum += Math.Abs(output - (ideal[i].Y - ideal[i - 1].Y) / ideal[i - 1].Y);
        }
        var error = absoluteErrorSum / (ideal.Count - numInputs);
        Trace.WriteLine("Error: " + error);
        return error;
      }, 30000, Math.Pow(10, -6), 12, 10);

      net = makeNet();
      net.SetWeightVector(report.MinimumLocation);
      var prediction = new List<Point>();
      for (int i = numInputs; i < ideal.Count; i++)
      {
        var output = net.Propagate(new double[] {
          normalizedNoisyValues[i - 1], normalizedNoisyValues[i - 2], normalizedNoisyValues[i - 3] });
        prediction.Add(new Point(ideal[i].X, noisyValues2[i - 1] * (1 + output)));
      }

      w.EqPlot.DrawLine(originalIdeal, Colors.Blue);
      w.EqPlot.DrawLine(noisy, Colors.Red);
      w.EqPlot.DrawLine(prediction, Colors.Green);
      w.Show();
    }

    private void GetVersaceDataButton_Click(object sender, RoutedEventArgs e)
    {
      Versace.GetData();
    }

    private void DIAButton_Click(object sender, RoutedEventArgs e)
    {
      var dia = Versace.GetCleanSeries().First(s => s.Symbol == "DIA");
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
  }
}