using System.Windows;

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
