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
    }

    public static void OptimizeMidpointStrat1(string symbol, string startDate, string endDate)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);

      var oParams = new List<OptimizerParameter> {
        //new OptimizerParameter("NumHidden", 2, 5, 1),
        //new OptimizerParameter("Lookback", 3, 10, 1)
        new OptimizerParameter("NumHidden", 3, 3, 1),
        new OptimizerParameter("Lookback", 6, 6, 1)
        };

      var reports = Strategy.Optimize("Midpoint", bars, oParams, OptimizationType.Anneal);
    }

    public static void OptimizeBuySell(string symbol, string startDate, string endDate)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);
      var oParams = new List<OptimizerParameter> {
        new OptimizerParameter("Activation1", 0, 0, 1),
        new OptimizerParameter("Activation2", 0, 0, 1),
        new OptimizerParameter("MomentumPeriod", 14, 14, 1)
      };
      var reports = Strategy.Optimize("BuySell", bars, oParams, OptimizationType.Anneal);
    }

    public static void DoBacktest(string symbol, string startDate, string endDate, string reportName, double initialValue, int marginFactor, bool isValidation)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);
      var optimizerReport = StrategyOptimizerReport.Load(reportName);
      var strat = Strategy.Make(reportName.Split('-')[0], optimizerReport.StrategyParams);
      strat.ApplyToBars(bars);
      var backtestReport = strat.Backtest(Genome.Load(optimizerReport.GenomeName),
        new Account { Equity = initialValue, MarginFactor = marginFactor, Padding = 50 });
      Trace.WriteLine(backtestReport.ToString());
      WriteTrades(backtestReport.Trades, DateTime.Now, optimizerReport.GenomeName);
      ShowBacktestChart(bars, backtestReport.Trades, initialValue, marginFactor, isValidation);
    }

    public static void DoGenomelessBacktest(string symbol, string startDate, string endDate, Strategy strat, double initialValue, int marginFactor, bool isValidation)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);
      strat.ApplyToBars(bars);
      var backtestReport = strat.Backtest(null, new Account { Equity = initialValue, MarginFactor = marginFactor, Padding = 50 });
      Trace.WriteLine(backtestReport.ToString());
      WriteTrades(backtestReport.Trades, DateTime.Now, "no-genome");
      ShowBacktestChart(bars, backtestReport.Trades, initialValue, marginFactor, isValidation);
    }

    static void ShowBacktestChart(DataSeries<Bar> bars, List<TradeRecord> trades, double initialValue, int marginFactor, bool isValidation)
    {
      var profitPctPerTrade = trades.ToDataSeries(t => t.PercentProfit * 100.0);
      var accountValue = trades.ToDataSeries(t => t.AccountValueAfterTrade);

      var w = new ChartWindow();
      var g1 = w.Chart.AddGraph();
      g1.Title = bars.Symbol;
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      g1.AddTrades(trades);
      var g1b = w.Chart.AddGraph();
      g1b.Title = "Momentum";
      g1b.Plots.Add(new Plot {
        DataSeries = bars.Closes().Momentum(14),
        Type = PlotType.ValueLine,
        Color = Brushes.Blue
      });
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

      w.Show();
    }

    private void UpdatingBuySell_Click(object sender, RoutedEventArgs e)
    {
      var sParams = List.Create(
        new StrategyParameter("Activation1", 0),
        new StrategyParameter("Activation2", 0),
        new StrategyParameter("MomentumPeriod", 14));
      DoGenomelessBacktest(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text,
        new UpdatingBuySellStrategy(sParams),
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
    }

    private void Swing_Click(object sender, RoutedEventArgs e)
    {
      DoGenomelessBacktest(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text,
        new SwingStrategy(new List<StrategyParameter>()),
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
    }

    private void SwingFlipped_Click(object sender, RoutedEventArgs e)
    {
      DoGenomelessBacktest(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text,
        new SwingStrategy(new List<StrategyParameter>(), true),
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
    }

    private void ShowMidpoint_Click(object sender, RoutedEventArgs e)
    {
      var bars = Data.Get(SymbolBox.Text);
      var report = StrategyOptimizerReport.Load(BuySellBox.Text);
      var strat = Strategy.Make(report.StrategyName, report.StrategyParams);
      strat.ApplyToBars(bars);
      var mid = strat.MakeSignal(Genome.Load(report.GenomeName));

      var w = new ChartWindow();
      var g1 = w.Chart.AddGraph();
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      g1.Plots.Add(new Plot {
        DataSeries = mid,
        Type = PlotType.Dash,
        Color = Brushes.BlueViolet
      });
      var g2 = w.Chart.AddGraph();
      var accuracy = bars.ZipElements<Value, Value>(mid, (s, m, v) =>
          (s[0].WaxBottom < m[0] && m[0] < s[0].WaxTop) ? 1 : 0);
      g2.Plots.Add(new Plot {
        DataSeries = accuracy,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });
      Trace.WriteLine("Accuracy: " + ((double)accuracy.Count(x => x.Val == 1) / accuracy.Length * 100));

      w.Show();
    }

    private void ShowMidFit_Click(object sender, RoutedEventArgs e)
    {
      var bars = Data.Get(SymbolBox.Text);

      var oParams = List.Create(
        new OptimizerParameter("LineSamples", 3, 3, 1),
        new OptimizerParameter("QuadSamples", 7, 7, 1)
        );

      Func<IEnumerable<StrategyParameter>, DataSeries<Value>, DataSeries<Value>> makeAccuracy = (sp, md) => {
        var maxSamplesNeeded = List.Create(sp.Get<int>("LineSamples"), sp.Get<int>("QuadSamples")).Max();
        return bars.ZipElements<Value, Value>(md, (s, m, v) =>
          (s.Pos > maxSamplesNeeded && (s[0].WaxBottom < m[0] && m[0] < s[0].WaxTop)) ? 1 : 0);
      };

      var reports = Optimizer.OptimizeStrategyParameters(oParams, sParams => {
        var strat = new MidFit(sParams);
        strat.ApplyToBars(bars);

        var acc = makeAccuracy(sParams, strat.MakeSignal(null));
        var accuracyPct = ((double)acc.Count(x => x.Val == 1) / acc.Length * 100);

        return new StrategyOptimizerReport {
          GenomeFitness = accuracyPct,
          StrategyParams = sParams,
          StrategyName = "MidFit",
          GenomeName = "none"
        };
      }).OrderByDescending(x => x.GenomeFitness);

      foreach (var r in reports)
        Trace.WriteLine(r.ToString());

      var st = new MidFit(reports.First().StrategyParams);
      st.ApplyToBars(bars);
      var mid = st.MakeSignal(null);

      var w = new ChartWindow();
      var g1 = w.Chart.AddGraph();
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      g1.Plots.Add(new Plot {
        DataSeries = mid,
        Type = PlotType.Dash,
        Color = Brushes.BlueViolet
      });
      var g2 = w.Chart.AddGraph();
      var accuracy = makeAccuracy(reports.First().StrategyParams, mid);
      g2.Plots.Add(new Plot {
        DataSeries = accuracy,
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });

      w.Show();
    }

    private void BacktestButton_Click(object sender, RoutedEventArgs e)
    {
      DoBacktest(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text, BuySellBox.Text,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
      DoBacktest(SymbolBox.Text, ValidationStartBox.Text, ValidationEndBox.Text, BuySellBox.Text,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), true);
    }

    static void WriteTrades(List<TradeRecord> trades, DateTime now, string genomeName)
    {
      var dirName = "Trades";
      if (!Directory.Exists(dirName))
        Directory.CreateDirectory(dirName);

      var fn = Path.Combine(dirName, string.Format("{0:yyyy-MM-dd-hh-mm-ss} {1}.csv", now, genomeName));

      using (var op = new StreamWriter(fn))
      {
        Action<IEnumerable<object>> writeRow = list => op.WriteLine(list.Join(","));

        writeRow(List.Create("Symbol", "Size", "EntryTime", "ExitTime", "Position", "Entry", "StopLimit", "Exit",
          "Profit", "Loss", "PercentProfit", "PercentLoss"));

        foreach (var t in trades)
          writeRow(List.Create<object>(t.Symbol, t.Size, t.EntryTime, t.ExitTime, t.PositionDirection, t.Entry, t.StopLimit, t.Exit,
            t.Profit, t.Loss, t.PercentProfit, t.PercentLoss));
      }
    }

    private void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
      OptimizeBuySell(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text);
    }

    private void OptimizeMidpointButton_Click(object sender, RoutedEventArgs e)
    {
      OptimizeMidpointStrat1(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text);
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e)
    {
      var window = new ChartWindow();
      var chart = window.Chart;
      var g = chart.AddGraph();
      g.Title = "QQQ";
      var qqq = Data.Get("QQQ");
      var tqqq = Data.Get("TQQQ");
      var ugaz = Data.Get("UGAZ");
      var qqq2 = qqq.From(DateTime.Parse("02/11/2010")).To(DateTime.Parse("06/29/2012")).Closes();
      var tqqq2 = tqqq.From(DateTime.Parse("02/11/2010")).To(DateTime.Parse("06/29/2012")).Closes();
      var qqq3 = qqq.Closes();
      var tqqq3 = tqqq.Closes();
      var faketqqq = qqq3.Derivative().MapElements<Value>((s, v) => 3 * s[0])
          .Integral(qqq3.First())/*.MapElements<Value>((s, v) => s[0] * tqqq2.Elements[0] / qqq2.Elements[0])*/;
      g.Plots.Add(new Plot {
        Title = "QQQ",
        Type = PlotType.ValueLine,
        Color = Brushes.Blue,
        DataSeries = qqq3
      });
      g.Plots.Add(new Plot {
        Title = "Calculated TQQQ",
        Type = PlotType.ValueLine,
        LineStyle = LineStyle.Dashed,
        Color = Brushes.Green,
        DataSeries = faketqqq
      });
      g.Plots.Add(new Plot {
        Type = PlotType.ValueLine,
        Color = Brushes.Red,
        LineStyle = LineStyle.Dashed,
        Title = "TQQQ",
        DataSeries = tqqq2
      });

      var g2 = chart.AddGraph();
      g2.Title = "% Return";
      g2.Plots.Add(new Plot {
        Type = PlotType.ValueLine,
        Color = Brushes.Blue,
        Title = "QQQ",
        DataSeries = qqq2.PercentReturn()
      });
      g2.Plots.Add(new Plot {
        Type = PlotType.ValueLine,
        Color = Brushes.Red,
        Title = "TQQQ",
        DataSeries = tqqq2.PercentReturn()
      });

      var z = qqq2.PercentReturn().ZipElements<Value, Value>(tqqq2.PercentReturn(), (sq, st, v) => sq[0] == 0 ? 0 : st[0] / sq[0]);
      var y = qqq3.PercentReturn().ZipElements<Value, Value>(faketqqq.PercentReturn(), (sq, st, v) => sq[0] == 0 ? 0 : st[0] / sq[0]);
      var g3 = chart.AddGraph();
      g3.Title = "";
      g3.Plots.Add(new Plot {
        Title = "TQQQ % Return / QQQ % Return",
        Type = PlotType.ValueLine,
        Color = Brushes.Plum,
        DataSeries = z
      });
      g3.Plots.Add(new Plot {
        Title = "Fake TQQQ % Return / QQQ % Return",
        Type = PlotType.ValueLine,
        Color = Brushes.Green,
        DataSeries = y
      });

      window.Show();
    }

    void ContributionsButton_Click(object sender, RoutedEventArgs e)
    {
      PrintContributions(BuySellBox.Text);
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
      var delayedHeikenAshi = tqqq.HeikenAshi().Delay(1);
      var window = new ChartWindow();
      var chart = window.Chart;

      //g.Plots.Add(new Plot {
      //  DataSeries = delayedHeikenAshi,
      //  Type = PlotType.Candlestick,
      //  CandleColors = CandleColors.WhiteBlack
      //});
      //g.Plots.Add(new Plot {
      //  DataSeries = tqqq.MapElements<Value>((s, v) => s[0].Open),
      //  Type = PlotType.Dash,
      //  Color = Brushes.Blue
      //});

      //var g2 = chart.AddGraph();
      //g2.Title = "Yesterday's HeikenAshi(TQQQ)";
      //g2.Plots.Add(new Plot {
      //  DataSeries = delayedHeikenAshi,
      //  Type = PlotType.Candlestick
      //});
      //g2.Plots.Add(new Plot {
      //  DataSeries = tqqq.DonchianMin(100),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Orange
      //});
      //g2.Plots.Add(new Plot {
      //  DataSeries = tqqq.DonchianMax(100),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Orange
      //});

      var undatedStates = new DataSeries<Value>(tqqq.Symbol,
        States.AssignStates(delayedHeikenAshi).Select(x => new Value(default(DateTime), (double)(int)x)));
      var states = tqqq.From(delayedHeikenAshi.First().Timestamp)
        .ZipElements<Value, Value>(undatedStates, (t, u, v) => u[0].Val);

      //var random = new Random();
      //var accuracy1 = tqqq.From(delayedHeikenAshi.First().Timestamp)
      //  .ZipElements<Bar, Value>(delayedHeikenAshi, (s, ha, v) => {
      //    var shouldBuy = ha[0].IsGreen;

      //    return shouldBuy == s[0].IsGreen ? 1 : 0;
      //  });

      //var g3 = chart.AddGraph();
      //g3.Title = "HeikenAshi Accuracy";
      //g3.Plots.Add(new Plot {
      //  DataSeries = accuracy1,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Blue
      //});
      //g3.Plots.Add(new Plot {
      //  DataSeries = tqqq.Closes().Momentum(14),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Blue
      //});

      var tqqqSkipFirst = tqqq.From(states.First().Timestamp);
      int nCorrect = 0;
      int nIncorrect = 0;
      double profit = 0;
      double loss = 0;
      DataSeries.Walk(tqqqSkipFirst, states, pos => {
        if (states[0] == 2)
        {
          if (tqqqSkipFirst[0].IsRed)
          {
            nCorrect++;
            profit += tqqqSkipFirst[0].WaxHeight();
          }
          else
          {
            nIncorrect++;
            loss += tqqqSkipFirst[0].WaxHeight();
          }
        }
      });

      Trace.WriteLine("parabolic accuracy: " + ((double)nCorrect / (nCorrect + nIncorrect) * 100.0) + "%");
      Trace.WriteLine("parabolic total profit/loss: " + ((double)profit / (double)loss));

      //var g4 = chart.AddGraph();
      //g4.Title = "DonchianPct";
      //g4.Plots.Add(new Plot {
      //  DataSeries = tqqq.DonchianPct(100, bar => bar.Close),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Blue
      //});

      //Trace.WriteLine("HeikinAshi Accuracy: " + ((double)accuracy1.Count(x => x.Val == 1) / accuracy1.Length * 100.0) + "%");


      //var ema = tqqq.Closes().EMA(45).Extrapolate();
      //var zlema = tqqq.Closes().ZLEMA(6).Extrapolate();
      //var doubleDiff = tqqq.Closes().ZLEMA(6).Derivative().Derivative().Extrapolate();
      //var slowMid = tqqq.Midpoint(b => b.WaxBottom, b => b.WaxTop).ZLEMA(10).Extrapolate();
      //var superSlowMid = tqqq.Midpoint(b => b.WaxBottom, b => b.WaxTop).ZLEMA(30).Extrapolate();
      //g.Plots.Add(new Plot {
      //  Title = "Extrapolated EMA(45)",
      //  DataSeries = ema,
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Purple
      //});
      //var g2 = chart.AddGraph();
      //g2.Plots.Add(new Plot {
      //  Title = "ZLEMA(6)''",
      //  DataSeries = doubleDiff,
      //  Type = PlotType.Bar,
      //  Color = Brushes.Blue
      //});
      //g.Plots.Add(new Plot {
      //  DataSeries = slowMid,
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.Blue,
      //  LineStyle = LineStyle.Dashed
      //});
      //g.Plots.Add(new Plot {
      //  DataSeries = mid.Average(slowMid),
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.OrangeRed
      //});
      //g.Plots.Add(new Plot {
      //  DataSeries = superSlowMid,
      //  Type = PlotType.ValueLine,
      //  Color = Brushes.LightBlue
      //});

      window.Show();
      chart.ScrollToEnd();

      DateTime currentEnd = tqqq.First().Timestamp.AddMonths(24);

      Action drawIt = () => {
        var bars = tqqq.To(currentEnd);
        chart.ClearGraphs();
        var g = chart.AddGraph();
        g.Title = "TQQQ";
        g.Plots.Add(new Plot {
          DataSeries = bars,
          Type = PlotType.Candlestick
        });
        var swingA = bars.Swing(4, false);
        g.Plots.Add(new Plot {
          DataSeries = swingA.MapElements<Value>((s, v) => s[0].Low).Delay(1),
          Type = PlotType.Dot,
          Color = Brushes.Orange
        });
        g.Plots.Add(new Plot {
          DataSeries = swingA.MapElements<Value>((s, v) => s[0].High).Delay(1),
          Type = PlotType.Dot,
          Color = Brushes.Green
        });
        //var swingB = bars.Swing(3);
        //g.Plots.Add(new Plot {
        //  DataSeries = swingB.MapElements<Value>((s, v) => s[0].Low).Delay(1),
        //  Type = PlotType.Circle,
        //  Color = Brushes.DarkOrange
        //});
        //g.Plots.Add(new Plot {
        //  DataSeries = swingB.MapElements<Value>((s, v) => s[0].High).Delay(1),
        //  Type = PlotType.Circle,
        //  Color = Brushes.Black
        //});

        var g3 = chart.AddGraph();
        g3.Plots.Add(new Plot {
          DataSeries = bars.Closes().Momentum(14).Delay(1),
          Type = PlotType.ValueLine,
          Color = Brushes.Blue
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
  }
}