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
      Action<Func<IEnumerable<StrategyParameter>, List<DataSeries<Value>>>, Func<Genome, IEnumerable<DataSeries<Value>>, DataSeries<Value>>> f)
    {
      Func<IEnumerable<StrategyParameter>, List<DataSeries<Value>>> cookInputs = sParams => {
        Func<Bar, double> zlemaComponent;
        if (sParams.Get<int>("ZLEMAOpenOrClose") == 0)
          zlemaComponent = bar => bar.Open;
        else
          zlemaComponent = bar => bar.Close;

        return List.Create(
          bars.MapElements<Value>((s, v) => s[1].Open / s[2].Close),
          bars.MapElements<Value>((s, v) => s[1].Low / s[2].Close),
          bars.MapElements<Value>((s, v) => s[1].High / s[2].Close),
          bars.MapElements<Value>((s, v) => s[1].Close / s[2].Close),
          bars.MapElements<Value>((s, v) => s[0].Open / s[2].Close),
          bars.ZLEMA(sParams.Get<int>("ZLEMAPeriod"), zlemaComponent).Derivative());
      };

      Func<Genome, IEnumerable<DataSeries<Value>>, DataSeries<Value>> makeSignal = (g, ins) =>
        bars.NeuralNet(new WardNet(ins.Count(), g), ins);

      f(cookInputs, makeSignal);
    }

    public static void OptimizeStrat1(string symbol, string startDate, string endDate)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);

      WithStrat1(bars, (cookInputs, makeSignal) => {

        var oParams = new List<OptimizerParameter> {
          new OptimizerParameter("ZLEMAPeriod", 5, 5, 1),
          new OptimizerParameter("ZLEMAOpenOrClose", 1, 1, 1),
        };

        var eParams = new EvolutionParams {
          NumGenerations = 100,
          GenerationSurvivorCount = 15,
          RandomAliensPerGeneration = 60,
          MaxOffspring = 1,
          MutationRate = 1,
          MaxMutationTimesVariance = 0.002,
        };

        var bestBuy = Optimizer.OptimizeNeuralIndicator(oParams, eParams, cookInputs, makeSignal, bars,
          bars.MapElements<Value>((s, v) => s[0].IsGreen ? 1 : -1));

        var bestStopLimit = Optimizer.OptimizeNeuralIndicator(oParams, eParams, cookInputs, makeSignal, bars,
          bars.MapElements<Value>((s, v) => s[0].IsGreen ? (s[0].Low / s[2].Close - 1) - 0.01 : (s[0].High / s[2].Close - 1) + 0.01));

        Trace.WriteLine("-- Buy Signal --------------");
        Trace.WriteLine(bestBuy.ToString());
        Trace.WriteLine("(Saved as " + bestBuy.Save("BuySell") + ")");
        Trace.WriteLine("----------------------------");

        Trace.WriteLine("-- StopLimit Signal --------------");
        Trace.WriteLine(bestStopLimit.ToString());
        Trace.WriteLine("(Saved as " + bestStopLimit.Save("StopLimit") + ")");
        Trace.WriteLine("----------------------------");
      });
    }

    private void BacktestButton_Click(object sender, RoutedEventArgs e)
    {
      BacktestStrat1(SymbolBox.Text, TeachStartBox.Text, TeachEndBox.Text, BuySellBox.Text, StopLimitBox.Text);
    }

    static BacktestReport BacktestStrat1(string symbol, string startDate, string endDate, string buySellName, string stopLimitName)
    {
      var bars = Data.Get(symbol).From(startDate).To(endDate);

      BacktestReport backtestReport = null;
      WithStrat1(bars, (cookInputs, makeSignal) => {

        var buySellReport = StrategyOptimizerReport.Load(buySellName);
        var stopLimitReport = StrategyOptimizerReport.Load(stopLimitName);

        var buySignal = makeSignal(Genome.Load(buySellReport.GenomeName), cookInputs(buySellReport.StrategyParams));
        var stopLimitSignal = makeSignal(Genome.Load(stopLimitReport.GenomeName), cookInputs(stopLimitReport.StrategyParams));

        double accountPadding = 20.0;
        var account = new Account { Equity = 10000, MarginFactor = 1 };
        var helper = BacktestHelper.Start(bars, account);

        var streams = List.Create<DataSeries>(bars, buySignal, stopLimitSignal);

        DataSeries.Walk(bars, buySignal, stopLimitSignal, pos => {
          if (pos < 2)
            return;

          var shouldBuy = buySignal[0] >= 0;
          var stopLimit = (1 + stopLimitSignal[0]) * bars[2].Close;
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

      var accountValue = new DataSeries<Value>(symbol, backtestReport.Trades.Select(t => new Value(t.ExitTime.Date, t.AccountValueAfterTrade)));

      var w = new ChartWindow();
      var g1 = w.Chart.AddGraph();
      g1.Title = "TQQQ";
      g1.Plots.Add(new Plot {
        DataSeries = bars,
        Type = PlotType.Candlestick
      });
      var g2 = w.Chart.AddGraph();
      g2.Plots.Add(new Plot {
        Title = "Account Value",
        DataSeries = accountValue,
        Type = PlotType.ValueLine,
        Color = Brushes.Green
      });
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
