using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Backtest;
using Quqe;
using PCW;

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

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      Go();
    }

    public static void Go()
    {
      var results = Optimizer.OptimizeStrategyParameters(
        new List<OptimizerParameter> {
          new OptimizerParameter("ZLEMAPeriod", 3, 15, 1),
          new OptimizerParameter("ActivationFunc", 0, 1, 1),
        }, sParams => {
          var buySellNet = new WardNet(sParams.Select(x => x.Name), "BuySignal");
          var stopLimitNet = new WardNet(sParams.Select(x => x.Name), "StopLimit");

          var eParams = new EvolutionParams {
            NumGenerations = 1000,
            GenerationSurvivorCount = 80,
            RandomAliensPerGeneration = 20,
            MaxOffspring = 4,
            MutationRate = 0.1,
            MaxMutation = 0.02,
          };
          var report = new OptimizerReport { StrategyParams = sParams, GenomeNames = new List<string>() };
          foreach (var net in List.Create(buySellNet, stopLimitNet))
          {
            var bestGenome = Optimizer.Evolve(eParams, net.ToGenome(), g => {
            });
            var genomeName = bestGenome.Save(net.Name);
            report.GenomeNames.Add(genomeName);
          }
          return report;
        });
    }
  }
}
