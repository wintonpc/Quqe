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

    public static void DoBacktest(string symbol, string startDate, string endDate, string strategyName, double initialValue, int marginFactor, bool isValidation)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);
      var strat = StrategyOptimizerReport.CreateStrategy(strategyName);
      var backtestReport = Strategy.BacktestSignal(bars, strat.MakeSignal(bars),
        new Account { Equity = initialValue, MarginFactor = marginFactor, Padding = 50 }, 2, null);
      Trace.WriteLine(backtestReport.ToString());
      ShowBacktestChart(bars, backtestReport.Trades, initialValue, marginFactor, isValidation, strategyName, strat.SParams);
    }

    public static void DoGenomelessBacktest(string symbol, string startDate, string endDate, Strategy strat, double initialValue, int marginFactor, bool isValidation)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);
      strat.ApplyToBars(bars);
      var backtestReport = strat.Backtest(null, new Account { Equity = initialValue, MarginFactor = marginFactor, Padding = 50 });
      Trace.WriteLine(backtestReport.ToString());
      Strategy.WriteTrades(backtestReport.Trades, DateTime.Now, "no-genome");
      ShowBacktestChart(bars, backtestReport.Trades, initialValue, marginFactor, isValidation, strat.Name, null);
    }

    static void ShowBacktestChart(DataSeries<Bar> bars, List<TradeRecord> trades, double initialValue, int marginFactor,
      bool isValidation, string strategyName, IEnumerable<StrategyParameter> sParams)
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
      g1.AddTrades(trades);
      var g2 = w.Chart.AddGraph();
      g2.Plots.Add(new Plot {
        Title = "Profit % per trade",
        DataSeries = profitPctPerTrade,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });
      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = string.Format("Initial Value: ${0:N0}   Margin: {1}", initialValue, marginFactor == 1 ? "none" : marginFactor + "x"),
        DataSeries = accountValue,
        Type = PlotType.ValueLine,
        Color = Brushes.Green,
        LineThickness = 2
      });

      if (strategyName.Split('-')[0] == "DTLRR2")
      {
        g1.Plots.Add(new Plot {
          Title = "TO",
          DataSeries = bars.Opens().LinReg(sParams.Get<int>("TOPeriod"), sParams.Get<int>("TOForecast")).Trim(0),
          Type = PlotType.ValueLine,
          Color = Brushes.Blue
        });
        g1.Plots.Add(new Plot {
          Title = "TC",
          DataSeries = bars.Closes().LinReg(sParams.Get<int>("TCPeriod"), sParams.Get<int>("TCForecast")).Delay(1).Trim(0),
          Type = PlotType.ValueLine,
          Color = Brushes.OrangeRed
        });
        g1.Plots.Add(new Plot {
          Title = "VO",
          DataSeries = bars.Opens().LinReg(sParams.Get<int>("VOPeriod"), sParams.Get<int>("VOForecast")).Trim(0),
          Type = PlotType.ValueLine,
          Color = Brushes.Aqua,
          LineStyle = LineStyle.Dashed
        });
        g1.Plots.Add(new Plot {
          Title = "VC",
          DataSeries = bars.Closes().LinReg(sParams.Get<int>("VCPeriod"), sParams.Get<int>("VCForecast")).Delay(1).Trim(0),
          Type = PlotType.ValueLine,
          Color = Brushes.Orange,
          LineStyle = LineStyle.Dashed
        });
      }

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

    private void BacktestButton_Click(object sender, RoutedEventArgs e)
    {
      DoBacktest(SymbolBox.Text, TrainingStartBox.Text, TrainingEndBox.Text, (string)StrategiesBox.SelectedItem,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
      Update();
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
      DoBacktest(SymbolBox.Text, ValidationStartBox.Text, ValidationEndBox.Text, (string)StrategiesBox.SelectedItem,
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
        new OptimizerParameter("MinMajority", 0.50, 0.70, 0.01),
        new OptimizerParameter("SmallMax", 0.0, 1.00, 0.02),
        new OptimizerParameter("MediumMax", 0.0, 2.00, 0.02),
        new OptimizerParameter("GapPadding", 0.0, 0.15, 0.01),
        new OptimizerParameter("SuperGapPadding", 0.0, 0.50, 0.01),
        new OptimizerParameter("EnableEma", 1, 1, 1),
        new OptimizerParameter("EmaPeriod", 3, 13, 1)
        );

      int lookback = oParams.Where(x => x.Name.EndsWith("Period")).Max(x => (int)x.High) + 1;

      Optimizer.OptimizeDecisionTree("DTCandles", oParams, 25000,
        DateTime.Parse(TrainingStartBox.Text), Data.Get(SymbolBox.Text).To(TrainingEndBox.Text),
        TimeSpan.FromDays(lookback), sParams => sParams.Get<double>("MinMajority"), DTCandlesStrategy.MakeExamples);

      Update();
    }
  }
}