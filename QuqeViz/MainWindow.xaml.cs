using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Quqe;
using PCW;
using System;
using StockCharts;
using System.Windows.Media;

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
      var symbol = "TQQQ";
      var s = Data.Get(symbol).From("02/12/2010").To("02/12/2012");
      Func<BacktestReport, double> goal = r => r.ProfitFactor;

      var results = Optimizer.OptimizeStrategyParameters(
        new List<OptimizerParameter> {
          new OptimizerParameter("ZLEMAPeriod", 3, 1, 1),
          new OptimizerParameter("ZLEMAOpenOrClose", 0, 0, 1),
        }, sParams => {
          Func<string, double> param = name => sParams.First(sp => sp.Name == name).Value;

          var eParams = new EvolutionParams {
            NumGenerations = 100,
            GenerationSurvivorCount = 5,
            RandomAliensPerGeneration = 5,
            MaxOffspring = 15,
            MutationRate = 0.7,
            MaxMutation = 0.05,
          };

          var zlemaSlope = s.ZLEMA((int)param("ZLEMAPeriod"), bar => param("ZLEMAOpenOrClose") == 0 ? bar.Open : bar.Close).Derivative();

          var inputNames = List.Create("Close1", "Open0", "ZLEMASlope");
          var outputNames = List.Create("BuySignal", "StopLimit");
          var bestGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(WardNet.GenomeSize(inputNames.Count, outputNames.Count)), g => {
            var net = new WardNet(inputNames, outputNames, g);
            var account = new Account { Equity = 10000, MarginFactor = 1 };
            var backtester = new Backtester(s, account);
            backtester.StartRun();
            backtester.UpdateAccountValue(account.Equity);
            double accountPadding = 20.0;

            DataSeries.Walk(s, zlemaSlope, pos => {
              if (pos == 0)
                return;
              var normal = s[1].Close;
              var normalizedPrices = List.Create(s[1].Close, s[0].Open).Select(x => x / normal).ToList();
              var inputs = normalizedPrices.Concat(List.Create(zlemaSlope[0].Val));
              var shouldBuy = net.Propagate(inputs)[0] >= 0;
              var stopLimit = net.Propagate(inputs)[1] * normal;
              var size = (int)((account.BuyingPower - accountPadding) / s[0].Open);
              if (size > 0)
              {
                if (shouldBuy)
                  account.EnterLong(symbol, size, new ExitOnSessionClose(Math.Max(0, stopLimit)), s.FromHere());
                else
                  account.EnterShort(symbol, size, new ExitOnSessionClose(Math.Min(100000, stopLimit)), s.FromHere());
              }
              backtester.UpdateAccountValue(account.Equity);
            });
            var report = backtester.StopRun();
            return goal(report);
          });
          var genomeName = bestGenome.Save();
          return new OptimizerReport {
            StrategyParams = sParams,
            GenomeName = genomeName
          };
        });

      results.ToList();
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
