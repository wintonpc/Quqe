using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using PCW;
using System.Collections.ObjectModel;
using Path = System.IO.Path;
using Quqe;
using System.Xml.Linq;
using System.Threading;
using System.Windows.Media;

namespace QuqeViz
{
  /// <summary>
  /// Interaction logic for BacktestWindow.xaml
  /// </summary>
  public partial class BacktestWindow : Window
  {
    BacktestPresentation Presentation;
    public BacktestWindow()
    {
      InitializeComponent();
      Loaded += delegate {
        Presentation = new BacktestPresentation();
        this.DataContext = Presentation;
      };
      Closed += delegate {
        Application.Current.Shutdown();
      };
    }

    private void VersaceResultListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (VersaceResultListBox.SelectedItem != null)
      {
        var h = (VersaceResultHolder)VersaceResultListBox.SelectedItem;
        Presentation.OnSelectedVersaceResult(h.ToString(), h.VersaceResult);
      }
    }

    private void VersaceSettingsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      if (VersaceSettingsListBox.SelectedItem != null)
        Presentation.OnSelectedVersaceSettings(((VersaceSettingsHolder)VersaceSettingsListBox.SelectedItem).VersaceSettings);
    }

    private void TrainButton_Click(object sender, RoutedEventArgs e)
    {
      Presentation.OnTrain();
    }

    private void BacktestButton_Click(object sender, RoutedEventArgs e)
    {
      Presentation.OnBacktest();
    }

    private void DumpButton_Click(object sender, RoutedEventArgs e)
    {
      Presentation.OnDump();
    }
  }

  public class VersaceResultHolder
  {
    public readonly VersaceResult VersaceResult;
    public VersaceResultHolder(VersaceResult vr)
    {
      VersaceResult = vr;
    }

    public string Name { get { return Path.GetFileNameWithoutExtension(VersaceResult.Path); } }

    public override string ToString()
    {
      return string.Format("F: {1:N1}%   ({0})", Name, VersaceResult.BestMixture.Fitness * 100);
    }
  }

  public class VersaceSettingsHolder
  {
    public readonly VersaceSettings VersaceSettings;
    public VersaceSettingsHolder(VersaceSettings vs)
    {
      VersaceSettings = vs;
    }

    public string Name { get { return Path.GetFileNameWithoutExtension(VersaceSettings.Path); } }

    public override string ToString()
    {
      return string.Format("{0}  ({1:MM/dd/yyyy} - {2:MM/dd/yyyy})", Name, VersaceSettings.StartDate, VersaceSettings.EndDate);
    }
  }

  public class BacktestPresentation : DependencyObject
  {
    DirectoryWatcher<VersaceResultHolder> ResultsWatcher;
    DirectoryWatcher<VersaceSettingsHolder> SettingsWatcher;

    public BacktestPresentation()
    {
      ResultsWatcher = new DirectoryWatcher<VersaceResultHolder>("VersaceResults", "*.xml", fn => new VersaceResultHolder(XSer.Read<VersaceResult>(XElement.Load(fn))));
      SettingsWatcher = new DirectoryWatcher<VersaceSettingsHolder>("VersaceSettings", "*.xml", fn => new VersaceSettingsHolder(Quqe.VersaceSettings.Load(fn)));
      SetupPropertyChangeHooks();
      OnSelectedVersaceSettings(Versace.Settings);
    }

    public ObservableCollection<VersaceResultHolder> VersaceResults { get { return ResultsWatcher.Items; } }
    public ObservableCollection<VersaceSettingsHolder> VersaceSettings { get { return SettingsWatcher.Items; } }

    public DateTime StartDate
    {
      get { return (DateTime)GetValue(StartDateProperty); }
      set { SetValue(StartDateProperty, value); }
    }
    public static readonly DependencyProperty StartDateProperty =
        DependencyProperty.Register("StartDate", typeof(DateTime), typeof(BacktestPresentation), new UIPropertyMetadata());

    public DateTime EndDate
    {
      get { return (DateTime)GetValue(EndDateProperty); }
      set { SetValue(EndDateProperty, value); }
    }
    public static readonly DependencyProperty EndDateProperty =
        DependencyProperty.Register("EndDate", typeof(DateTime), typeof(BacktestPresentation), new UIPropertyMetadata());

    public int TestingSplitPct
    {
      get { return (int)GetValue(TestingSplitPctProperty); }
      set { SetValue(TestingSplitPctProperty, value); }
    }
    public static readonly DependencyProperty TestingSplitPctProperty =
        DependencyProperty.Register("TestingSplitPct", typeof(int), typeof(BacktestPresentation), new UIPropertyMetadata(75));

    public bool UseValidationSet
    {
      get { return (bool)GetValue(UseValidationSetProperty); }
      set { SetValue(UseValidationSetProperty, value); }
    }
    public static readonly DependencyProperty UseValidationSetProperty =
        DependencyProperty.Register("UseValidationSet", typeof(bool), typeof(BacktestPresentation), new UIPropertyMetadata(false));

    public int ValidationSplitPct
    {
      get { return (int)GetValue(ValidationSplitPctProperty); }
      set { SetValue(ValidationSplitPctProperty, value); }
    }
    public static readonly DependencyProperty ValidationSplitPctProperty =
        DependencyProperty.Register("ValidationSplitPct", typeof(int), typeof(BacktestPresentation), new UIPropertyMetadata(75));

    public DateTime TestingDate
    {
      get { return (DateTime)GetValue(TestingDateProperty); }
      set { SetValue(TestingDateProperty, value); }
    }
    public static readonly DependencyProperty TestingDateProperty =
        DependencyProperty.Register("TestingDate", typeof(DateTime), typeof(BacktestPresentation), new UIPropertyMetadata());
    void RefreshTestingDate(object sender, EventArgs ea)
    {
      TestingDate = StartDate.AddDays((int)(EndDate.Subtract(StartDate).TotalDays * TestingSplitPct / 100.0));
    }

    public DateTime ValidationDate
    {
      get { return (DateTime)GetValue(ValidationDateProperty); }
      set { SetValue(ValidationDateProperty, value); }
    }
    public static readonly DependencyProperty ValidationDateProperty =
        DependencyProperty.Register("ValidationDate", typeof(DateTime), typeof(BacktestPresentation), new UIPropertyMetadata());
    void RefreshValidationDate(object sender, EventArgs ea)
    {
      ValidationDate = StartDate.AddDays((int)(TestingDate.Subtract(StartDate).TotalDays * ValidationSplitPct / 100.0));
    }

    void SetupPropertyChangeHooks()
    {
      HookPropChange(TestingSplitPctProperty, RefreshTestingDate);
      HookPropChange(StartDateProperty, RefreshTestingDate);
      HookPropChange(EndDateProperty, RefreshTestingDate);
      HookPropChange(ValidationSplitPctProperty, RefreshValidationDate);
      HookPropChange(StartDateProperty, RefreshValidationDate);
      HookPropChange(TestingDateProperty, RefreshValidationDate);
    }

    void HookPropChange(DependencyProperty dp, EventHandler handler)
    {
      DependencyPropertyDescriptor.FromProperty(dp, typeof(BacktestPresentation)).AddValueChanged(this, handler);
    }

    public string SelectedMixtureName
    {
      get { return (string)GetValue(SelectedMixtureNameProperty); }
      set { SetValue(SelectedMixtureNameProperty, value); }
    }
    public static readonly DependencyProperty SelectedMixtureNameProperty =
        DependencyProperty.Register("SelectedMixtureName", typeof(string), typeof(BacktestPresentation), new UIPropertyMetadata(""));

    VMixture SelectedMixture;
    internal void OnSelectedVersaceResult(string name, VersaceResult r)
    {
      SelectedMixture = r.BestMixture;
      SelectedMixtureName = name;
      OnSelectedVersaceSettings(r.VersaceSettings);
    }

    public string SettingsDescription
    {
      get { return (string)GetValue(SettingsDescriptionProperty); }
      set { SetValue(SettingsDescriptionProperty, value); }
    }
    public static readonly DependencyProperty SettingsDescriptionProperty =
        DependencyProperty.Register("SettingsDescription", typeof(string), typeof(BacktestPresentation), new UIPropertyMetadata(""));

    VersaceSettings SelectedSettings;
    internal void OnSelectedVersaceSettings(VersaceSettings s)
    {
      StartDate = s.StartDate;
      EndDate = s.EndDate;
      TestingSplitPct = s.TestingSplitPct;
      UseValidationSet = s.UseValidationSet;
      ValidationSplitPct = s.ValidationSplitPct;
      SelectedSettings = s.Clone();
      SettingsDescription = SelectedSettings.ToString();
    }

    internal void OnTrain()
    {
      var fitnessChart = new EqPlotWindow();
      fitnessChart.Show();
      var diversityChart = new EqPlotWindow();
      diversityChart.Show();
      var mainSync = SyncContext.Current;
      Action<List<PopulationInfo>> updateHistoryWindow = populationHistory => {
        mainSync.Post(() => {
          var fitnessHistory = populationHistory.Select(pi => pi.Fitness).ToList();
          fitnessChart.EqPlot.Clear(Colors.White);
          fitnessChart.EqPlot.Bounds = new Rect(0, fitnessHistory.Min(), fitnessHistory.Count, fitnessHistory.Max() - fitnessHistory.Min());
          fitnessChart.EqPlot.DrawLine(List.Repeat(fitnessHistory.Count, i => new Point(i, fitnessHistory[i])), Colors.Blue);

          var diversityHistory = populationHistory.Select(pi => pi.Diversity).ToList();
          diversityChart.EqPlot.Clear(Colors.White);
          diversityChart.EqPlot.Bounds = new Rect(0, diversityHistory.Min(), diversityHistory.Count, diversityHistory.Max() - diversityHistory.Min());
          diversityChart.EqPlot.DrawLine(List.Repeat(diversityHistory.Count, i => new Point(i, diversityHistory[i])), Colors.DarkOrange);
        });
      };

      SetVersaceSettings();
      Versace.Train(updateHistoryWindow, result => {
        fitnessChart.Title = new VersaceResultHolder(result).ToString();
      });
    }

    private void SetVersaceSettings()
    {
      SelectedSettings.StartDate = StartDate;
      SelectedSettings.EndDate = EndDate;
      SelectedSettings.TestingSplitPct = TestingSplitPct;
      SelectedSettings.UseValidationSet = UseValidationSet;
      SelectedSettings.ValidationSplitPct = ValidationSplitPct;
      Versace.Settings = SelectedSettings;
    }

    internal void OnBacktest()
    {
      Versace.Settings = null; // check that the backtest code doesn't reference Settings
      var ss = SelectedSettings;

      var testingData = Versace.GetPreprocessedValues(ss.PreprocessingType, ss.PredictedSymbol, ss.TestingStart, ss.TestingEnd, false);
      var report = VersaceBacktest.Backtest(ss.PredictionType, SelectedMixture, new Account { Equity = 10000, MarginFactor = 1, Padding = 40 },
        Versace.GetPreprocessedValues(ss.PreprocessingType, ss.PredictedSymbol, ss.TrainingStart, ss.ValidationEnd, false).Inputs,
        testingData.Inputs, testingData.Predicted);
    }

    internal void OnDump()
    {
      SelectedMixture.Dump();
    }
  }

  public class DateTimeToDateConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      return ((DateTime)value).ToString("MM/dd/yyyy");
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      return DateTime.Parse((string)value).Date;
    }
  }

  public class BoolToHiddenConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      return (bool)value ? Visibility.Visible : Visibility.Hidden;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      return value.Equals(Visibility.Visible);
    }
  }

}
