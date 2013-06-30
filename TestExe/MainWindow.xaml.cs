using System.Windows;

namespace TestExe
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
      Loaded += MainWindow_Loaded;
    }

    void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
      var testFixture = new QuqeTest.EvolutionTests();
      //testFixture.NoEvolveLeaks();
    }
  }
}
