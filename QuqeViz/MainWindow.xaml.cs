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

    //public static void Go()
    //{
    //  var tqqq = Data.Get("TQQQ").From("02/12/2010").To("02/12/2012");
    //  Func<BacktestReport, double> goal = r => r.CPC;

    //  var results = Optimizer.OptimizeStrategyParameters(
    //    new List<OptimizerParameter> {
    //      new OptimizerParameter("ZLEMAPeriod", 3, 1, 1),
    //      new OptimizerParameter("ZLEMAOpenOrClose", 0, 0, 1),
    //    }, sParams => {
    //      var eParams = new EvolutionParams {
    //        NumGenerations = 1000,
    //        GenerationSurvivorCount = 15,
    //        RandomAliensPerGeneration = 60,
    //        MaxOffspring = 1,
    //        MutationRate = 1,
    //        MaxMutationTimesVariance = 0.002,
    //      };

    //      Strategy strat = new OnePerDayStrategy1(sParams, tqqq);

    //      var bestGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(WardNet.GenomeSize(strat.InputNames, strat.OutputNames)), g => {
    //        var report = strat.Backtest(new WardNet(strat.InputNames, strat.OutputNames, g));
    //        return goal(report);
    //      });

    //      var genomeName = bestGenome.Save();
    //      return new OptimizerReport {
    //        StrategyParams = sParams,
    //        GenomeName = genomeName
    //      };
    //    });

    //  results.ToList();

    //  foreach (var r in results)
    //  {

    //  }
    //}

    public static void Go()
    {
      var bars = Data.Get("TQQQ").From("02/12/2010").To("02/12/2012");
      var oParams = new List<OptimizerParameter> {
        new OptimizerParameter("ZLEMAPeriod", 3, 1, 1),
        new OptimizerParameter("ZLEMAOpenOrClose", 0, 0, 1),
      };
      var eParams = new EvolutionParams {
        NumGenerations = 1000,
        GenerationSurvivorCount = 15,
        RandomAliensPerGeneration = 60,
        MaxOffspring = 1,
        MutationRate = 1,
        MaxMutationTimesVariance = 0.002,
      };
      var reports = Optimizer.FullOptimize(oParams, eParams, sParams => new OnePerDayStrategy1(sParams, bars), report => report.CPC);
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
