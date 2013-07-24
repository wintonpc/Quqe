using System.Diagnostics;
using System.Text;
using Lz4Net;
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
      //var testFixture = new QuqeTest.EvolutionTests();
      //testFixture.NoEvolveLeaks();
      var originalText = "I came here for an argument";
      var originalBytes = Encoding.UTF8.GetBytes(originalText);
      var compressed = Lz4.CompressBytes(originalBytes);
      var decompressed = Lz4.DecompressBytes(compressed);
      var decompressedText = Encoding.UTF8.GetString(decompressed);
      var decompressTry = Lz4.DecompressBytes(originalBytes);
      Trace.WriteLine("original    : " + originalText);
      Trace.WriteLine("decompressed: " + decompressedText);
    }
  }
}
