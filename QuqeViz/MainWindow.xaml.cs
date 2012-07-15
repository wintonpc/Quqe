using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Quqe;
using PCW;
using System;
using StockCharts;
using System.Windows.Media;
using System.IO;

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

    public static void Go()
    {
      var bars = Data.Get("TQQQ").From("02/12/2010").To("02/12/2012");

      var oParams = new List<OptimizerParameter> {
        new OptimizerParameter("ZLEMAPeriod", 5, 5, 1),
        new OptimizerParameter("ZLEMAOpenOrClose", 1, 1, 1),
      };

      var eParams = new EvolutionParams {
        NumGenerations = 250,
        GenerationSurvivorCount = 15,
        RandomAliensPerGeneration = 60,
        MaxOffspring = 1,
        MutationRate = 1,
        MaxMutationTimesVariance = 0.002,
      };

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

      var buyReports = Optimizer.OptimizeNeuralIndicator(oParams, eParams, cookInputs, makeSignal, bars,
        bars.MapElements<Value>((s, v) => s[0].IsGreen ? 1 : -1));

      WriteReport(buyReports, (sParams, genome) => {

        double accountPadding = 20.0;

        var account = new Account { Equity = 10000, MarginFactor = 1 };
        var helper = BacktestHelper.Start(bars, account);

        var buySignal = makeSignal(genome, cookInputs(sParams));

        DataSeries.Walk(bars, signal, pos => {
          if (pos == 0)
            return;
          var normal = s[1].Close;
          var normalizedPrices = List.Create(s[1].Open, s[1].Low, s[1].High, s[1].Close, s[0].Open).Select(x => x / normal).ToList();
          var inputs = normalizedPrices.Concat(List.Create(zlemaSlope[UseZLEMAOpen ? 0 : 1].Val));
          var shouldBuy = net.Propagate(inputs)[0] >= 0;
          var stopLimit = (1 + net.Propagate(inputs)[1]) * normal;
          var size = (int)((account.BuyingPower - accountPadding) / s[0].Open);
          if (size > 0)
          {
            if (shouldBuy)
              account.EnterLong(Bars.Symbol, size, new ExitOnSessionClose(Math.Max(0, stopLimit)), s.FromHere());
            else
              account.EnterShort(Bars.Symbol, size, new ExitOnSessionClose(Math.Min(100000, stopLimit)), s.FromHere());
          }
          helper.UpdateAccountValue(account.Equity);
        });
        return helper.Stop();
      });

      var stopLimitReport = Optimizer.OptimizeNeuralIndicator(oParams, eParams, cookInputs, makeSignal, bars,
        bars.MapElements<Value>((s, v) => s[0].IsGreen ? s[0].Low - 0.01 : s[0].High + 0.01));
    }

    static void WriteReport(IEnumerable<StrategyOptimizerReport> reports, Func<IEnumerable<StrategyParameter>, Genome, BacktestReport> backtest)
    {
      var dirName = "Reports";
      if (!Directory.Exists(dirName))
        Directory.CreateDirectory(dirName);

      var now = DateTime.Now;

      var fn = Path.Combine(dirName, string.Format("{0:yyyy-MM-dd-hh-mm-ss}.csv", now));

      using (var op = new StreamWriter(fn))
      {
        Action<IEnumerable<object>> writeRow = list => op.WriteLine(list.Join(","));

        var header = new List<string>();
        foreach (var sp in reports.First().StrategyParams)
          header.Add(sp.Name);
        header.Add("Genome");
        header.AddRange(List.Create("ProfitFactor", "CPC", "MaxDrawdownPercent", "NumWinningTrades", "NumLosingTrades",
          "AverageWin", "AverageLoss", "WinningTradeFraction", "AverageWinLossRatio"));
        writeRow(header);

        foreach (var optimizerReport in reports)
        {
          var r = backtest(optimizerReport.StrategyParams, Genome.Load(optimizerReport.GenomeName));

          var row = optimizerReport.StrategyParams.Select(sp => sp.Value).Cast<object>().ToList();
          row.Add(optimizerReport.GenomeName);
          row.AddRange(List.Create<object>(r.ProfitFactor, r.CPC, r.MaxDrawdownPercent, r.NumWinningTrades, r.NumLosingTrades,
            r.AverageWin, r.AverageLoss, r.WinningTradeFraction, r.AverageWinLossRatio));
          writeRow(row);

          WriteTrades(r.Trades, now, optimizerReport.GenomeName);
        }
      }
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

    private void GoButton_Click(object sender, RoutedEventArgs e)
    {
      Go();
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e)
    {
      var window = new ChartWindow();
      var chart = window.Chart;
      var pres = chart.Presentation;
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
