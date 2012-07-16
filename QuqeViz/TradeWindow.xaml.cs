using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Quqe;
using StockCharts;
using PCW;

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
        DataSeries = new DataSeries<Bar>(t.Symbol, List.Create(bar)),
        Type = PlotType.Candlestick
      });
    }
  }
}
