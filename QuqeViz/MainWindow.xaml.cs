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
      var signal = strat.MakeSignal(
          DateTime.Parse(TrainingStartBox.Text), bars.To(TrainingEndBox.Text),
          DateTime.Parse(ValidationStartBox.Text), bars.To(ValidationEndBox.Text));
      var backtestReport = Strategy.BacktestSignal(bars, signal,
        new Account { Equity = initialValue, MarginFactor = marginFactor, Padding = 20 }, 0, null);
      Trace.WriteLine(string.Format("Training  :  {0}  -  {1}", TrainingStartBox.Text, TrainingEndBox.Text));
      bool validationWarning = DateTime.Parse(ValidationStartBox.Text) <= DateTime.Parse(TrainingEndBox.Text);
      Trace.WriteLine(string.Format("Validation:  {0}  -  {1}{2}",
        ValidationStartBox.Text, ValidationEndBox.Text, validationWarning ? " !!!!!" : ""));
      if (strat is DTStrategy)
        EvalAndDumpDTStrategy((DTStrategy)strat);
      Trace.WriteLine(backtestReport.ToString());
      ShowBacktestChart(bars, backtestReport.Trades, signal, initialValue, marginFactor, isValidation, strategyName, strat.SParams);
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

    static void ShowBacktestChart(DataSeries<Bar> bars, List<TradeRecord> trades, DataSeries<Value> signal,
      double initialValue, int marginFactor, bool isValidation, string strategyName, IEnumerable<StrategyParameter> sParams)
    {
      var profitPctPerTrade = trades.ToDataSeries(t => t.PercentProfit * 100.0);
      var accountValue = trades.ToDataSeries(t => t.AccountValueAfterTrade);

      var w = new ChartWindow();
      w.Title = bars.Symbol + " : " + (!isValidation ? "Training" : "Validation");
      var g1 = w.Chart.AddGraph();
      g1.Title = bars.Symbol;
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      g1.Plots.Add(new Plot {
        DataSeries = bars.Closes().LinReg(2, 1).Delay(1),
        Type = PlotType.ValueLine,
        Color = Brushes.Blue
      });
      g1.Plots.Add(new Plot {
        DataSeries = bars.Closes().LinReg(7, 1).Delay(1),
        Type = PlotType.ValueLine,
        Color = Brushes.Red
      });
      g1.AddTrades(trades);

      var g4 = w.Chart.AddGraph();
      g4.Plots.Add(new Plot {
        Title = "RSquared(10)",
        DataSeries = bars.Closes().RSquared(10).Delay(1),
        Type = PlotType.ValueLine,
        Color = Brushes.Red
      });
      g4.Plots.Add(new Plot {
        DataSeries = bars.ConstantLine(0.75),
        Type = PlotType.ValueLine,
        Color = Brushes.GreenYellow
      });

      var g5 = w.Chart.AddGraph();
      g5.Plots.Add(new Plot {
        Title = "LinRegSlope(4)",
        DataSeries = bars.Closes().LinRegSlope(4).Delay(1),
        Type = PlotType.Bar,
        Color = Brushes.Gray
      });

      var g2 = w.Chart.AddGraph();
      g2.Plots.Add(new Plot {
        Title = "Profit % per trade",
        DataSeries = profitPctPerTrade,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });
      var g2b = w.Chart.AddGraph();
      g2b.Plots.Add(new Plot {
        Title = "Signal",
        DataSeries = signal,
        Type = PlotType.Bar,
        Color = Brushes.Purple
      });
      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = string.Format("Initial Value: ${0:N0}   Margin: {1}", initialValue, marginFactor == 1 ? "none" : marginFactor + "x"),
        DataSeries = accountValue,
        Type = PlotType.ValueLine,
        Color = Brushes.Green,
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
        var g2 = chart.AddGraph();
        double barSumThresh = 1.0 / 3;
        g2.Plots.Add(new Plot {
          Title = "EMA Slope",
          DataSeries = bars.Closes().ZLEMA(12).Derivative().Sign().Delay(1),
          Type = PlotType.Bar,
          Color = Brushes.LightPink
        });
        g2.Plots.Add(new Plot {
          DataSeries = bars.ConstantLine(barSumThresh),
          Type = PlotType.ValueLine,
          Color = Brushes.Orange
        });
        g2.Plots.Add(new Plot {
          DataSeries = bars.ConstantLine(-barSumThresh),
          Type = PlotType.ValueLine,
          Color = Brushes.Orange
        });
        g2.Plots.Add(new Plot {
          Title = "BarSum",
          DataSeries = bars.BarSum3(10, 20, 2).Delay(1),
          Type = PlotType.Bar,
          Color = Brushes.CornflowerBlue
        });
        g2.Plots.Add(new Plot {
          Title = "BarSum",
          DataSeries = bars.MostCommonBarColor(8).MapElements<Value>((s, v) => s[0] * 0.5).Delay(1),
          Type = PlotType.Bar,
          Color = new SolidColorBrush(Color.FromArgb(128, 255, 10, 100))
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
      // exhaustive
      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 1, 1),
      //  new OptimizerParameter("WL", 0, 1, 1),
      //  new OptimizerParameter("WH", 0, 1, 1),
      //  new OptimizerParameter("WC", 0, 1, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 3, 1),
      //  new OptimizerParameter("SlowRegPeriod", 4, 9, 1),
      //  new OptimizerParameter("RSquaredPeriod", 5, 11, 2),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
      //  new OptimizerParameter("LinRegSlopePeriod", 2, 10, 2)
      //  );
      // closes only
      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 0, 1),
      //  new OptimizerParameter("WL", 0, 0, 1),
      //  new OptimizerParameter("WH", 0, 0, 1),
      //  new OptimizerParameter("WC", 1, 1, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 2, 1),
      //  new OptimizerParameter("SlowRegPeriod", 7, 7, 1),
      //  new OptimizerParameter("RSquaredPeriod", 9, 9, 1),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
      //  new OptimizerParameter("LinRegSlopePeriod", 6, 6, 1),
      //  new OptimizerParameter("ATRPeriod", 10, 10, 1),
      //  new OptimizerParameter("TrendBreakThresh", 0.55, 0.55, 0.01)
      //  );

      // annealing
      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 2, 1),
      //  new OptimizerParameter("WL", 0, 2, 1),
      //  new OptimizerParameter("WH", 0, 2, 1),
      //  new OptimizerParameter("WC", 0, 2, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 2, 1),
      //  new OptimizerParameter("SlowRegPeriod", 3, 9, 1),
      //  new OptimizerParameter("RSquaredPeriod", 5, 15, 1),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.01),
      //  new OptimizerParameter("LinRegSlopePeriod", 5, 15, 1)
      //  );

      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 0, 1),
      //  new OptimizerParameter("WL", 0, 0, 1),
      //  new OptimizerParameter("WH", 0, 1, 1),
      //  new OptimizerParameter("WC", 1, 1, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 2, 1),
      //  new OptimizerParameter("SlowRegPeriod", 4, 9, 1),
      //  new OptimizerParameter("RSquaredPeriod", 9, 11, 1),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
      //  new OptimizerParameter("LinRegSlopePeriod", 4, 10, 1),
      //  new OptimizerParameter("ATRPeriod", 9, 11, 1),
      //  new OptimizerParameter("TrendBreakThresh", 0.45, 0.65, 0.01)
      //  );

      // by hand
      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 0, 1),
      //  new OptimizerParameter("WL", 0, 0, 1),
      //  new OptimizerParameter("WH", 0, 0, 1),
      //  new OptimizerParameter("WC", 1, 1, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 2, 1),
      //  new OptimizerParameter("SlowRegPeriod", 7, 7, 1),
      //  new OptimizerParameter("RSquaredPeriod", 10, 10, 1),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
      //  new OptimizerParameter("LinRegSlopePeriod", 7, 7, 1),
      //  new OptimizerParameter("ATRPeriod", 0, 0, 1),
      //  new OptimizerParameter("TrendBreakThresh", 1, 1, 0.01)
      //  );
      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 1, 1),
      //  new OptimizerParameter("WL", 0, 0, 1),
      //  new OptimizerParameter("WH", 0, 0, 1),
      //  new OptimizerParameter("WC", 1, 1, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 7, 1),
      //  new OptimizerParameter("SlowRegPeriod", 8, 30, 1),

      //  // long trends
      //  new OptimizerParameter("RSquaredPeriod", 10, 10, 1),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
      //  new OptimizerParameter("LinRegSlopePeriod", 7, 7, 1),

      //  // trend-reversing gaps
      //  new OptimizerParameter("ATRPeriod", 10, 10, 1),
      //  new OptimizerParameter("TrendBreakThresh", 1, 1, 0.5)
      //  );

      // with validation window averaging
      //var oParams = List.Create(
      //  new OptimizerParameter("WO", 0, 0, 1),
      //  new OptimizerParameter("WL", 0, 0, 1),
      //  new OptimizerParameter("WH", 0, 0, 1),
      //  new OptimizerParameter("WC", 1, 1, 1),
      //  new OptimizerParameter("FastRegPeriod", 2, 5, 1),
      //  new OptimizerParameter("SlowRegPeriod", 6, 10, 2),

      //  // long trends
      //  new OptimizerParameter("RSquaredPeriod", 8, 12, 2),
      //  new OptimizerParameter("RSquaredThresh", 0.75, 0.75, 0.02),
      //  new OptimizerParameter("LinRegSlopePeriod", 2, 12, 2),

      //  // trend-reversing gaps
      //  new OptimizerParameter("ATRPeriod", 10, 10, 1),
      //  new OptimizerParameter("TrendBreakThresh", 0.10, 2.00, 0.30)
      //  );
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
        new OptimizerParameter("LinRegSlopePeriod", 4, 4, 1)
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

      //Func<IEnumerable<StrategyParameter>, double> calcFitness = sParams => {
      //  var bars = Data.Get(symbol).From(start).To(end);
      //  var strat = new Trending1Strategy(sParams);
      //  var signal = strat.MakeSignal(default(DateTime), null, bars.First().Timestamp, bars);
      //  //var bt = Strategy.BacktestSignal(bars, signal,
      //  //  new Account { Equity = 15000, MarginFactor = 1, Padding = 20 }, 0, null);
      //  return bars.SignalAccuracyPercent(signal);
      //};

      Func<IEnumerable<StrategyParameter>, double> calcFitness = sParams => {
        var bars = Data.Get(symbol).From(start).To(end);
        TimeSpan sampleSize = TimeSpan.FromDays(100);
        double fitnessSum = 0;
        int windowCount = 0;
        for (DateTime windowStart = bars.First().Timestamp;
          windowStart.Add(sampleSize) < bars.Last().Timestamp;
          windowStart = windowStart.Add(sampleSize))
        {
          var strat = new Trending1Strategy(sParams);
          var bs = bars.From(windowStart).To(windowStart.Add(sampleSize).AddDays(-1));
          var signal = strat.MakeSignal(default(DateTime), null, bs.First().Timestamp, bs);
          fitnessSum += bs.SignalAccuracyPercent(signal);
          windowCount++;
        }
        return fitnessSum / windowCount;
      };

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
      var trades = OTPDStrategy.GetTrades();
      var profit = trades.ToDataSeries(t => t.PercentProfit * 300.0);
      var signal = trades.ToDataSeries(t => t.PositionDirection == PositionDirection.Long ? 1 : -1);

      Trace.WriteLine("Accuracy: " + ((double)trades.Count(t => t.IsWin) / trades.Count));

      var w = new ChartWindow();
      var g = w.Chart.AddGraph();
      g.Plots.Add(new Plot {
        Title = "QQQ",
        DataSeries = Data.Get("QQQ").From("06/07/2010").To("07/18/2012"),
        Type = PlotType.Candlestick
      });
      g.AddTrades(trades);
      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = "Profit % per trade",
        DataSeries = profit,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });
      var g2 = w.Chart.AddGraph();
      g2.Plots.Add(new Plot {
        Title = "Signal",
        DataSeries = signal,
        Type = PlotType.Bar,
        Color = Brushes.Purple
      });

      w.Show();
    }
  }
}