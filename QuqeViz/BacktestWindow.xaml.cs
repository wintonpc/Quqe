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
using System.ComponentModel;
using PCW;
using System.Collections.ObjectModel;
using Path = System.IO.Path;
using Quqe;

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
    }

    private void VersaceResultListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      Presentation.SetSelectedVersaceResult((VersaceResultHolder)VersaceResultListBox.SelectedItem);
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

  public class BacktestPresentation : DependencyObject
  {
    DirectoryWatcher<VersaceResultHolder> Watcher;

    public BacktestPresentation()
    {
      Watcher = new DirectoryWatcher<VersaceResultHolder>("VersaceResults", "*.xml", fn => new VersaceResultHolder(VersaceResult.Load(fn)));

      SetupPropertyChangeHooks();
    }

    public ObservableCollection<VersaceResultHolder> VersaceResults { get { return Watcher.Items; } }

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

    public VersaceResultHolder SelectedVersaceResult
    {
      get { return (VersaceResultHolder)GetValue(SelectedVersaceResultProperty); }
      set { SetValue(SelectedVersaceResultProperty, value); }
    }
    public static readonly DependencyProperty SelectedVersaceResultProperty =
        DependencyProperty.Register("SelectedVersaceResult", typeof(VersaceResultHolder), typeof(BacktestPresentation), new UIPropertyMetadata());

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

    internal void SetSelectedVersaceResult(VersaceResultHolder versaceResultHolder)
    {
      SelectedVersaceResult = versaceResultHolder;
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
