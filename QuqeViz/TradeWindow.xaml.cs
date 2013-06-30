using System.Windows;
using Quqe;
using StockCharts;

namespace QuqeViz
{
  /// <summary>
  /// Interaction logic for TradeWindow.xaml
  /// </summary>
  public partial class TradeWindow : Window
  {
    public TradeWindow()
    {
      InitializeComponent();
    }

    void SetTrade(TradeRecord t, Bar bar)
    {
      Chart.ClearGraphs();
      var g = Chart.AddGraph();
      g.Plots.Add(new Plot {
        DataSeries = new DataSeries<Bar>(t.Symbol, Lists.Create(bar)),
        Type = PlotType.Candlestick
      });
    }
  }
}
