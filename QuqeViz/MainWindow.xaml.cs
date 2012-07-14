using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Backtest;
using Quqe;
using PCW;
using System;

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
      var account = new Account { Equity = 10000, MarginFactor = 6 };
      var symbol = "TQQQ";
      var backtester = new Backtester(symbol, DateTime.Parse("02/12/2010"), DateTime.Parse("02/12/2012"), account);

      var results = Optimizer.OptimizeStrategyParameters(
        new List<OptimizerParameter> {
          new OptimizerParameter("ZLEMAPeriod", 3, 15, 1),
          new OptimizerParameter("ZLEMAOpenOrClose", 0, 1, 1),
        }, sParams => {
          Func<string, double> param = name => sParams.First(sp => sp.Name == name).Value;

          var eParams = new EvolutionParams {
            NumGenerations = 1000,
            GenerationSurvivorCount = 80,
            RandomAliensPerGeneration = 20,
            MaxOffspring = 4,
            MutationRate = 0.1,
            MaxMutation = 0.02,
          };

          var inputNames = List.Create("Close1", "Open0", "ZLEMASlope");
          var bestBuyGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(WardNet.GenomeSize(inputNames.Count)), g => {
            return 0; // ???
          });
          var bestStopLimitGenome = Optimizer.Evolve(eParams, Optimizer.MakeRandomGenome(WardNet.GenomeSize(inputNames.Count)), g => {
            return 0; // ???
          });
          var n1 = bestBuyGenome.Save("BuySignal");
          var n2 = bestStopLimitGenome.Save("StopLimit");
          return new OptimizerReport {
            StrategyParams = sParams,
            GenomeNames = List.Create(n1, n2)
          };
        });
    }
  }
}
