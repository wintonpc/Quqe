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

    static void WithStrat1(DataSeries<Bar> bars,
      Action<
      Func<IEnumerable<StrategyParameter>, List<DataSeries<Value>>>,
      Func<Genome, IEnumerable<DataSeries<Value>>, DataSeries<Value>>,
      Func<DataSeries<Bar>, double>
      > f)
    {
      Func<DataSeries<Bar>, double> getNormal = bs => bs[3].Close;

      Func<IEnumerable<StrategyParameter>, List<DataSeries<Value>>> cookInputs = sParams => {
        return List.Create(
          bars.MapElements<Value>((s, v) => s[2].Open / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[2].Low / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[2].High / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[2].Close / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[1].Open / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[1].Low / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[1].High / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[1].Close / getNormal(s)),
          bars.MapElements<Value>((s, v) => s[0].Open / getNormal(s)),
          bars.ZLEMA(sParams.Get<int>("SlowZLEMAPeriod"), bar => bar.Close).Derivative(),
          bars.ZLEMA(sParams.Get<int>("FastZLEMAPeriod"), bar => bar.Close).Derivative());
      };

      Func<Genome, IEnumerable<DataSeries<Value>>, DataSeries<Value>> makeSignal = (g, ins) =>
        bars.NeuralNet(new WardNet(ins.Count(), g), ins);

      f(cookInputs, makeSignal, getNormal);
    }

    public static void OptimizeStrat1(string symbol, string startDate, string endDate)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);

      WithStrat1(bars, (cookInputs, makeSignal, getNormal) => {

        var oParams = new List<OptimizerParameter> {
          new OptimizerParameter("SlowZLEMAPeriod", 3, 10, 1),
          new OptimizerParameter("FastZLEMAPeriod", 11, 41, 3)
        };

        var eParams = new EvolutionParams {
          NumGenerations = 2500,
          GenerationSurvivorCount = 15,
          RandomAliensPerGeneration = 60,
          MaxOffspring = 1,
          MutationRate = 1,
          MaxMutationTimesVariance = 0.002,
        };

        var buyReports = Optimizer.OptimizeNeuralIndicator(oParams, eParams, cookInputs, makeSignal, bars,
          bars.MapElements<Value>((s, v) => s[0].IsGreen ? 1 : -1));

        var stopLimitReports = Optimizer.OptimizeNeuralIndicator(oParams, eParams, cookInputs, makeSignal, bars,
          bars.MapElements<Value>((s, v) => s[0].IsGreen ? (s[0].Low / getNormal(s) - 1) - 0.03 : (s[0].High / getNormal(s) - 1) + 0.03));

        Trace.WriteLine("=== BEST ===");
        var bestBuy = buyReports.First();
        Trace.WriteLine("-- Buy Signal --------------");
        Trace.WriteLine(bestBuy.ToString());
        Trace.WriteLine("(Saved as " + bestBuy.Save("BuySell") + ")");
        Trace.WriteLine("----------------------------");

        var bestStopLimit = stopLimitReports.First();
        Trace.WriteLine("-- StopLimit Signal --------------");
        Trace.WriteLine(bestStopLimit.ToString());
        Trace.WriteLine("(Saved as " + bestStopLimit.Save("StopLimit") + ")");
        Trace.WriteLine("----------------------------");

        Trace.WriteLine("=== ALL ===");
        Action<IEnumerable<StrategyOptimizerReport>> printReports = list => {
          foreach (var r in list)
            Trace.WriteLine(r.ToString() + "\r\n");
        };

        Trace.WriteLine("-- Buy Signal --------------");
        printReports(buyReports);
        Trace.WriteLine("-- StopLimit Signal --------------");
        printReports(stopLimitReports);
      });
    }

    private void BacktestButton_Click(object sender, RoutedEventArgs e)
    {
      BacktestStrat1(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text, BuySellBox.Text, StopLimitBox.Text,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), false);
    }

    private void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
      BacktestStrat1(SymbolBox.Text, ValidationStartBox.Text, ValidationEndBox.Text, BuySellBox.Text, StopLimitBox.Text,
        double.Parse(InitialValueBox.Text), int.Parse(MarginFactorBox.Text), true);
    }

    static BacktestReport BacktestStrat1(string symbol, string startDate, string endDate, string buySellName, string stopLimitName, double initialValue, int marginFactor, bool isValidation)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);

      BacktestReport backtestReport = null;
      WithStrat1(bars, (cookInputs, makeSignal, getNormal) => {

        var buySellReport = StrategyOptimizerReport.Load(buySellName);
        var stopLimitReport = StrategyOptimizerReport.Load(stopLimitName);

        var buySignal = makeSignal(Genome.Load(buySellReport.GenomeName), cookInputs(buySellReport.StrategyParams));
        var stopLimitSignal = makeSignal(Genome.Load(stopLimitReport.GenomeName), cookInputs(stopLimitReport.StrategyParams));

        double accountPadding = 50.0;
        var account = new Account { Equity = initialValue, MarginFactor = marginFactor };
        var helper = BacktestHelper.Start(bars, account);

        var streams = List.Create<DataSeries>(bars, buySignal, stopLimitSignal);

        DataSeries.Walk(bars, buySignal, stopLimitSignal, pos => {
          if (pos < 3)
            return;

          var shouldBuy = buySignal[0] >= 0;
          var stopLimit = (1 + stopLimitSignal[0]) * getNormal(bars);

          // correct bad stop limits
          //double maxLossPercent = Math.Abs((bars[1].Open - bars[1].Close) / Math.Min(bars[1].Open, bars[1].Close)) * 3;
          double maxLossPercent = 0.08;
          var liberalLongStop = bars[0].Open * (1 - maxLossPercent);
          var liberalShortStop = bars[0].Open * (1 + maxLossPercent);
          if (shouldBuy/* && (stopLimit >= bars[0].Open || stopLimit < liberalLongStop)*/)
            stopLimit = liberalLongStop;
          else if (!shouldBuy/* && (stopLimit <= bars[0].Open || stopLimit > liberalShortStop)*/)
            stopLimit = liberalShortStop;

          // special stops for gaps 2
          double minGapPct = 0.03;
          //double harshness = 6;
          double stopGapFraction = 0.8;
          if (shouldBuy && bars[1].IsGreen && bars[1].WaxTop * (1 + minGapPct) < bars[0].Open) // buying gap up
          {
            //var gapPct = (bars[0].Open - bars[1].WaxTop) / bars[1].WaxTop;
            //var stopGapFraction = Math.Pow(Math.Exp(-gapPct), harshness);
            stopLimit = bars[0].Open - (bars[0].Open - bars[1].WaxTop) * stopGapFraction;
          }
          if (!shouldBuy && bars[1].IsRed && bars[1].WaxBottom * (1 - minGapPct) > bars[0].Open) // selling gap down
          {
            //var gapPct = (bars[1].WaxBottom - bars[0].Open) / bars[0].Open;
            //var stopGapFraction = Math.Pow(Math.Exp(-gapPct), harshness);
            stopLimit = bars[0].Open + (bars[1].WaxBottom - bars[0].Open) * stopGapFraction;
          }

          var size = (int)((account.BuyingPower - accountPadding) / bars[0].Open);

          if (size > 0)
          {
            if (shouldBuy)
              account.EnterLong(bars.Symbol, size, new ExitOnSessionClose(Math.Max(0, stopLimit)), bars.FromHere());
            else
              account.EnterShort(bars.Symbol, size, new ExitOnSessionClose(Math.Min(100000, stopLimit)), bars.FromHere());
          }
        });
        backtestReport = helper.Stop();

        Trace.WriteLine(backtestReport.ToString());

        WriteTrades(backtestReport.Trades, DateTime.Now, buySellReport.GenomeName + ", " + stopLimitReport.GenomeName);
      });

      var w = new ChartWindow();
      w.Chart.Title = isValidation ? "Validation" : "Backtest";
      var g1 = w.Chart.AddGraph();
      g1.Title = "TQQQ";
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      g1.Plots.Add(new Plot {
        DataSeries = backtestReport.Trades.ToDataSeries(t => t.StopLimit),
        Type = PlotType.Dash,
        Color = Brushes.Blue
      });
      foreach (var t in backtestReport.Trades)
        g1.Trades.Add(t);
      var g3 = w.Chart.AddGraph();
      g3.Plots.Add(new Plot {
        Title = "Profit % per trade",
        DataSeries = new DataSeries<Value>(symbol, backtestReport.Trades.Select(t => new Value(t.ExitTime.Date, t.PercentProfit * 100))),
        Type = PlotType.Bar,
        Color = Brushes.Blue
      });

      var g2 = w.Chart.AddGraph();
      g2.Title = "Initial Value: " + initialValue + ", Margin: " + marginFactor + "x";
      g2.Plots.Add(new Plot {
        Title = "Account Value",
        DataSeries = new DataSeries<Value>(symbol, backtestReport.Trades.Select(t => new Value(t.ExitTime.Date, t.AccountValueAfterTrade))),
        Type = PlotType.ValueLine,
        Color = Brushes.Green
      });

      //var g4 = w.Chart.AddGraph();
      //g4.Plots.Add(new Plot {
      //  Title = "Wrong-sided stoplimits",
      //  DataSeries = new DataSeries<Value>(symbol, backtestReport.Trades.Select(t => {
      //    if ((t.PositionDirection == PositionDirection.Long && t.StopLimit > t.Entry)
      //      || (t.PositionDirection == PositionDirection.Short && t.StopLimit < t.Entry))
      //      return new Value(t.EntryTime.Date, 1);
      //    else
      //      return new Value(t.EntryTime.Date, 0);
      //  })),
      //  Type = PlotType.Bar,
      //  Color = Brushes.Red
      //});

      w.Show();

      return backtestReport;
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
      OptimizeStrat1(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text);
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
  }
}
