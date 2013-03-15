using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace QuqeViz
{
  /// <summary>
  /// Interaction logic for EqPlotWindow.xaml
  /// </summary>
  public partial class EqPlotWindow : Window
  {
    public EqPlotWindow()
    {
      InitializeComponent();
    }

    public EqPlot ThePlot { get { return EqPlot; } }
  }
}
