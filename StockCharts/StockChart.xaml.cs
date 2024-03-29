﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Quqe;

namespace StockCharts
{
  /// <summary>
  /// Interaction logic for StockChart.xaml
  /// </summary>
  public partial class StockChart : UserControl
  {
    public readonly StockChartPresentation Presentation;
    public Graph TimeAxisGraph;
    public StockChart()
    {
      InitializeComponent();
      Presentation = new StockChartPresentation(this);
      this.DataContext = Presentation;
    }

    public Graph AddGraph()
    {
      var g = new Graph(Presentation);
      Presentation.Graphs.Add(g);
      return g;
    }

    public void ClearGraphs()
    {
      Presentation.Graphs.Clear();
    }

    public void RestructureGraphsGrid()
    {
      GraphsGrid.Children.Clear();
      GraphsGrid.RowDefinitions.Clear();

      var rowIndex = 0;
      foreach (var g in Presentation.Graphs)
      {
        GraphsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 18 });
        g.SetValue(Grid.RowProperty, rowIndex);
        g.SetValue(Grid.ColumnProperty, 0);
        GraphsGrid.Children.Add(g);
        rowIndex++;

        if (g != Presentation.Graphs.Last())
        {
          GraphsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
          var splitter = new GridSplitter {
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch
          };
          splitter.SetValue(Grid.RowProperty, rowIndex);
          g.SetValue(Grid.ColumnProperty, 0);
          //var onMouseOver = new EventTrigger {  Property = GridSplitter.IsMouseOverProperty, Value = true };
          //onMouseOver.Setters.Add(new Setter { Property = GridSplitter.BackgroundProperty, Value = Brushes.Black });
          //splitter.Triggers.Add(onMouseOver);
          //var onMouseOut = new Trigger { Property = GridSplitter.IsMouseOverProperty, Value = false };
          //onMouseOut.Setters.Add(new Setter { Property = GridSplitter.BackgroundProperty, Value = Brushes.DarkGray });
          //splitter.Triggers.Add(onMouseOut);
          GraphsGrid.Children.Add(splitter);
          rowIndex++;
        }
      }

      GraphsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
      if (TimeAxisGraph == null)
        TimeAxisGraph = new Graph(Presentation) { DrawTimeAxis = true };
      TimeAxisGraph.SetValue(Grid.RowProperty, rowIndex);
      TimeAxisGraph.SetValue(Grid.ColumnProperty, 0);
      GraphsGrid.Children.Add(TimeAxisGraph);
    }

    public void ScrollToEnd()
    {
      GraphScrollBar.Value = GraphScrollBar.Maximum;
    }

    public string Title
    {
      get { return Presentation.Title; }
      set { Presentation.Title = value; }
    }

    public event Action NavigatePrev;
    public event Action NavigateNext;

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
      NavigatePrev.Fire();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
      NavigateNext.Fire();
    }
  }

  public class StockChartPresentation : DependencyObject, INotifyPropertyChanged
  {
    readonly StockChart Parent;
    public StockChartPresentation(StockChart parent)
    {
      Parent = parent;
      Period = PeriodType.OneDay;
      Graphs = new ObservableCollection<Graph>();
      PlotBoundaries = new Dictionary<Plot, PlotInfo>();
      MinSlotPxWidth = 7;
      Graphs.CollectionChanged += delegate { parent.RestructureGraphsGrid(); };
      Parent.GraphScrollBar.ValueChanged += delegate { RedrawGraphs(); };
      Parent.ZoomScrollBar.ValueChanged += delegate { RedrawGraphs(); };
      parent.MouseMove += new MouseEventHandler(parent_MouseMove);
      parent.MouseLeave += new MouseEventHandler(parent_MouseLeave);
    }

    void parent_MouseMove(object sender, MouseEventArgs e)
    {
      foreach (var g in Graphs)
        g.OnHoverStarted(e.GetPosition(g));
    }

    void parent_MouseLeave(object sender, MouseEventArgs e)
    {
      foreach (var g in Graphs)
        g.OnHoverEnded();
    }

    public string Title { get; set; }
    public ObservableCollection<Graph> Graphs { get; set; }
    public int ScrollBarMax { get { return TotalSlots - SlotsInView; } }
    public int TotalSlots { get; private set; }
    public double SlotPxWidth { get; private set; }
    public int SlotsInView { get; set; }
    public PeriodType Period { get; private set; }
    public int MinSlotPxWidth { get; set; }
    public DateTime MinDate { get; private set; }
    public DateTime MaxDate { get; private set; }
    public int SlotOffset
    {
      get { return (int)Math.Min(Parent.GraphScrollBar.Value, ScrollBarMax); }
    }
    public Dictionary<Plot, PlotInfo> PlotBoundaries { get; private set; }
    public double AvailableWidth { get { return Math.Max(0, Parent.GraphsGrid.ActualWidth - VerticalAxisWidth); } }
    readonly double VerticalAxisWidth = 32;
    public readonly double HorizontalAxisHeight = 16;
    List<DateTime> _Timestamps;
    public List<DateTime> Timestamps { get { return _Timestamps; } }

    public string CursorText
    {
      get { return (string)GetValue(CursorTextProperty); }
      set { SetValue(CursorTextProperty, value); }
    }
    public static readonly DependencyProperty CursorTextProperty =
        DependencyProperty.Register("CursorText", typeof(string), typeof(StockChartPresentation), new UIPropertyMetadata(""));

    public void RedrawGraphs()
    {
      if (!Graphs.Any())
        return;

      MinDate = Graphs.Min(g => g.Plots.Min(p => p.DataSeries.Elements.ToArray().First().Timestamp));
      MaxDate = Graphs.Max(g => g.Plots.Max(p => p.DataSeries.Elements.ToArray().Last().Timestamp));

      if (Period == PeriodType.OneDay)
        TotalSlots = CalcTotalSlots(out _Timestamps);
      else
        throw new Exception("didn't expect " + Period);

      SlotPxWidth = Math.Max(MinSlotPxWidth, AvailableWidth / TotalSlots);
      SlotsInView = (int)Math.Floor(AvailableWidth / SlotPxWidth);

      if (SlotsInView == 0)
        return;

      foreach (var g in Graphs)
        g.Redraw();
      Parent.TimeAxisGraph.Redraw();

      Update();
    }

    int CalcTotalSlots(out List<DateTime> timestamps)
    {
      var plots = Graphs.SelectMany(g => g.Plots).OrderBy(p => p.DataSeries.Elements.ToArray().First().Timestamp).ToArray();

      var z = Lists.Mesh(plots, p => p.DataSeries.Elements, dse => dse.Timestamp, dt => dt.AddDays(1), (ps, dses) => new { Plots = ps, Elements = dses.ToList() })
        .Where(x => x.Plots.Any()).Select(x => new { Plots = x.Plots, Elements = x.Elements, Timestamp = x.Elements.First().Timestamp }).ToList();

      timestamps = z.Select(x => x.Timestamp).ToList();

      foreach (var p in plots)
      {
        var left = z.FindIndex(x => x.Plots.Contains(p));
        PlotBoundaries[p] = new PlotInfo { Left = left, Right = left + p.DataSeries.Length - 1 };
      }
      return z.Count;
    }

    int CalcTotalSlots2(out List<DateTime> timestamps)
    {
      timestamps = new List<DateTime>();
      var plots = Graphs.SelectMany(g => g.Plots).OrderBy(p => p.DataSeries.Elements.ToArray().First().Timestamp).ToArray();
      if (plots.Length == 0)
        return 0;
      else
      {
        int count = 0;
        for (int i = 0; i < plots.Length - 1; i++)
        {
          PlotBoundaries[plots[i]] = new PlotInfo { Left = count, Right = count + plots[i].DataSeries.Elements.ToArray().Count() - 1 };

          //DateTime nextFirst = plots[i + 1].DataSeries.Elements.ToArray().First().Timestamp;
          DateTime? nextFirst = null;
          var nextExtender = plots.Skip(i).FirstOrDefault(p =>
            p.DataSeries.Elements.Last().Timestamp > plots[i].DataSeries.Elements.Last().Timestamp);
          if (nextExtender != null)
            nextFirst = nextExtender.DataSeries.Elements.First().Timestamp;
          foreach (var el in plots[i].DataSeries.Elements.ToArray())
          {
            if (nextFirst.HasValue && el.Timestamp == nextFirst.Value)
              break;
            else
            {
              timestamps.Add(el.Timestamp);
              count++;
            }
          }
        }
        timestamps.AddRange(plots.Last().DataSeries.Elements.ToArray().Select(e => e.Timestamp));
        var lastCount = plots.Last().DataSeries.Elements.ToArray().Count();
        PlotBoundaries[plots.Last()] = new PlotInfo { Left = count, Right = count + lastCount - 1 };
        count += lastCount;
        return count;
      }
    }

    void Update()
    {
      if (PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(""));
    }

    public event PropertyChangedEventHandler PropertyChanged;
  }

  public class PlotInfo
  {
    public int Left;
    public int Right;
    public int ClipLeft;
    public int ClipRight;
  }
}
