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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using Quqe;

namespace StockCharts
{
  /// <summary>
  /// Interaction logic for Graph.xaml
  /// </summary>
  public partial class Graph : UserControl
  {
    public Graph(StockChartPresentation parentChart)
    {
      ParentChart = parentChart;
      Plots = new ObservableCollection<Plot>();
      InitializeComponent();
      this.DataContext = this;
      Loaded += delegate { RedrawNeeded(); };
      SizeChanged += delegate { RedrawNeeded(); };
      MouseMove += new MouseEventHandler(Graph_MouseMove);
      Plots.CollectionChanged += delegate { RedrawNeeded(); };
    }

    StockChartPresentation ParentChart;
    int SlotOffset { get { return ParentChart.SlotOffset; } }
    DateTime MinDate { get { return ParentChart.MinDate; } }
    int TotalSlots { get { return ParentChart.TotalSlots; } }
    int SlotsInView { get { return ParentChart.SlotsInView; } }
    Dictionary<Plot, PlotInfo> PlotBoundaries { get { return ParentChart.PlotBoundaries; } }
    double AvailableHeight { get { return DrawTimeAxis ? Math.Max(0, GraphCanvas.ActualHeight - ParentChart.HorizontalAxisHeight) : GraphCanvas.ActualHeight; } }

    void RedrawNeeded()
    {
      ParentChart.RedrawGraphs();
    }

    void Graph_MouseMove(object sender, MouseEventArgs e)
    {

    }

    public string Title { get; set; }
    public ObservableCollection<Plot> Plots { get; set; }
    public bool DrawTimeAxis { get; set; }

    Rect ViewRegion;

    public void Redraw()
    {
      double minVal = double.PositiveInfinity;
      double maxVal = double.NegativeInfinity;

      bool anythingVisible = false;
      foreach (var p in Plots)
      {
        var pb = PlotBoundaries[p];
        pb.ClipLeft = Math.Max(SlotOffset, pb.Left);
        pb.ClipRight = Math.Min(SlotOffset + SlotsInView - 1, pb.Right);
        var ds = p.DataSeries.Elements.ToArray();
        for (int i = pb.ClipLeft; i <= pb.ClipRight; i++)
        {
          minVal = Math.Min(minVal, ds[i - pb.Left].Min);
          maxVal = Math.Max(maxVal, ds[i - pb.Left].Max);
          anythingVisible = true;
        }
      }

      if (!anythingVisible)
        minVal = maxVal = 0;

      GraphCanvas.Children.Clear();

      ViewRegion = new Rect(-0.5, minVal, (double)SlotsInView, maxVal - minVal);

      if (anythingVisible)
      {
        var axisInfo = CalcAxisInfo(minVal, maxVal);

        AddGridLines(axisInfo);

        foreach (var p in Plots)
        {
          var pb = PlotBoundaries[p];

          if (p.Type == PlotType.ValueLine)
          {
            var path = new Path();
            path.Stroke = p.Color;
            path.StrokeThickness = 1;
            SetLineStyle(path, p.LineStyle);

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
              var es = ((DataSeries<Value>)p.DataSeries).ToArray();
              for (int i = pb.ClipLeft; i < pb.ClipRight; i++)
              {
                int k = i - pb.Left;
                if (i == pb.ClipLeft)
                  ctx.BeginFigure(PointToCanvas(i - SlotOffset, es[k], ViewRegion), false, false);
                else
                  ctx.LineTo(PointToCanvas(i + 1 - SlotOffset, es[k + 1], ViewRegion), true, true);
              }
            }

            geom.Freeze();
            path.Data = geom;
            GraphCanvas.Children.Add(path);
          }
          else if (p.Type == PlotType.Candlestick)
          {
            var bars = ((DataSeries<Bar>)p.DataSeries).ToArray();
            for (int i = pb.ClipLeft; i <= pb.ClipRight; i++)
            {
              int k = i - pb.Left;
              var bar = bars[k];
              var brush = bar.Close < bar.Open ?
                //new SolidColorBrush(Color.FromRgb(255, 15, 35)) :
                Brushes.Red :
                new SolidColorBrush(Color.FromRgb(25, 210, 0));
              //Brushes.Red : Brushes.Green;
              AddLine(i - SlotOffset, bar.High, i - SlotOffset, bar.Low, brush, LineStyle.Solid, 1, ViewRegion);
              var p1 = PointToCanvas(i - SlotOffset, bar.Open, ViewRegion);
              var p2 = PointToCanvas(i - SlotOffset, bar.Close, ViewRegion);
              AddRectangle(
                p1.X - Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                p1.Y,
                p2.X + Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                p2.Y,
                brush);
            }
          }
        }

        AddVerticalAxis(axisInfo);
      }


      // time axis
      if (DrawTimeAxis)
        AddTimeAxis();

      AddText(Title, 0, 0, "bold");
      double yOffset = 0;
      foreach (var p in Plots.Where(x => x.Type == PlotType.ValueLine))
      {
        yOffset += 16;
        AddText(p.Title, 16, yOffset, "normal");
        AddLine(2, yOffset + 9, 14, yOffset + 9, p.Color, LineStyle.Solid, 2);
      }
    }

    Line CrosshairLineV;
    Line CrosshairLineH;
    public void ShowCrosshairLine(Point canvasPoint)
    {
      //var lineColor = new SolidColorBrush(Color.FromRgb(173, 40, 230));
      var lineColor = Brushes.LightBlue;

      HideCrosshairLine();
      var p = PointFromCanvas(canvasPoint.X, canvasPoint.Y, ViewRegion);
      var slotNumber = (int)Math.Round(p.X);
      if (slotNumber < 0 || slotNumber >= ParentChart.SlotsInView) // stray point?
        return;
      var snappedPoint = PointToCanvas(slotNumber, 0, ViewRegion);
      CrosshairLineV = new Line {
        X1 = snappedPoint.X,
        Y1 = 0,
        X2 = snappedPoint.X,
        Y2 = AvailableHeight,
        Stroke = lineColor,
        StrokeThickness = 1
      };
      RenderOptions.SetEdgeMode(CrosshairLineV, EdgeMode.Aliased);
      GraphCanvas.Children.Add(CrosshairLineV);

      if (0 <= canvasPoint.Y && canvasPoint.Y <= AvailableHeight)
      {
        CrosshairLineH = new Line {
          X1 = 0,
          Y1 = canvasPoint.Y,
          X2 = ParentChart.AvailableWidth,
          Y2 = canvasPoint.Y,
          Stroke = lineColor,
          StrokeThickness = 1
        };
        RenderOptions.SetEdgeMode(CrosshairLineH, EdgeMode.Aliased);
        GraphCanvas.Children.Add(CrosshairLineH);

        var sb = new StringBuilder();
        sb.Append(ParentChart.Timestamps[slotNumber + SlotOffset].ToString("MMM d, yyyy"));
        foreach (var plot in Plots)
        {
          var pb = PlotBoundaries[plot];
          var absoluteSlotNumber = slotNumber + SlotOffset;
          if (absoluteSlotNumber < pb.ClipLeft || pb.ClipRight < absoluteSlotNumber)
            continue;
          if (plot.Type == PlotType.ValueLine)
          {
            double v = ((DataSeries<Value>)plot.DataSeries).ToArray()[absoluteSlotNumber - pb.Left];
            sb.AppendFormat("   {0}: {1:N2}", plot.Title, v);
          }
          else if (plot.Type == PlotType.Candlestick)
          {
            var bar = ((DataSeries<Bar>)plot.DataSeries).ToArray()[absoluteSlotNumber - pb.Left];
            sb.AppendFormat("   OLHC: {0:N2}, {1:N2}, {2:N2}, {3:N2}", bar.Open, bar.Low, bar.High, bar.Close);
          }
        }
        ParentChart.CursorText = sb.ToString();
      }
    }

    public void HideCrosshairLine()
    {
      if (CrosshairLineV != null)
      {
        GraphCanvas.Children.Remove(CrosshairLineV);
        CrosshairLineV = null;
      }
      if (CrosshairLineH != null)
      {
        GraphCanvas.Children.Remove(CrosshairLineH);
        CrosshairLineH = null;
      }
    }

    void AddTimeAxis()
    {
      if (ParentChart.Period == PeriodType.OneDay)
      {
        //TimeDelta deltaType;
        //if (ParentChart.SlotsInView < 10)
        //  deltaType = TimeDelta.Day;
        //else if (ParentChart.SlotsInView < 70)
        //  deltaType = TimeDelta.Week;
        //else
        //  deltaType = TimeDelta.Month;

        AddLine(0, AvailableHeight, GraphCanvas.ActualWidth, AvailableHeight, Brushes.Black);

        var tss = ParentChart.Timestamps;
        var next = tss[SlotOffset];
        var last = tss[SlotOffset + SlotsInView - 1];
        next = next.AddMonths(1);
        next = new DateTime(next.Year, next.Month, 1);
        while (next <= last)
        {
          var trial = next;
          while (tss.IndexOf(trial) == -1)
            trial = trial.AddDays(1);
          var p = PointToCanvas(tss.IndexOf(trial) - SlotOffset, 0, ViewRegion);
          AddLine(p.X, AvailableHeight, p.X, AvailableHeight + 3, Brushes.Black);
          AddText(trial.ToString("MMM \\'yy"), p.X - 16, AvailableHeight + 1, "normal", 10);
          next = next.AddMonths(1);
          //if (deltaType == TimeDelta.Day)
          //  next = next.AddDays(1);
          //else if (deltaType == TimeDelta.Week)
          //  next = next.AddDays(7);
          //else if (deltaType == TimeDelta.Month)
          //  next = next.AddMonths(1);
        }
      }
    }

    enum TimeDelta { Day, Week, Month }

    class AxisInfo
    {
      public double First;
      public double Last;
      public double Delta;
      public int RoundingPower;
    }

    AxisInfo CalcAxisInfo(double minVal, double maxVal)
    {
      var range = maxVal - minVal;
      var targetAxisPxSpacing = 50;
      int numTicks = (int)(AvailableHeight / targetAxisPxSpacing);
      var rawDelta = range / numTicks;

      var p = (int)Math.Floor(Math.Log10(rawDelta));
      var roundDelta = RoundToPowerOfTen(rawDelta, p, Math.Round);
      var error = range % roundDelta;
      var roundFirst = RoundToPowerOfTen(minVal + error / 2, p, Math.Ceiling);

      return new AxisInfo {
        First = roundFirst,
        Last = maxVal,
        Delta = roundDelta,
        RoundingPower = p
      };
    }

    static double RoundToPowerOfTen(double n, int power, Func<double, double> roundMethod)
    {
      var m = Math.Pow(10, power);
      return roundMethod(n / m) * m;
    }

    void AddVerticalAxis(AxisInfo axis)
    {
      var axisColor = Brushes.Black;
      double xOffset = ParentChart.AvailableWidth;
      double tickOffset = xOffset + 1;
      double edgeOffset = xOffset + 5;
      double textOffset = xOffset + 7;
      AddLine(edgeOffset, 0, edgeOffset, GraphCanvas.ActualHeight, axisColor);
      for (double n = axis.First; n <= axis.Last; n += axis.Delta)
      {
        var p = PointToCanvas(0, n, ViewRegion);
        AddLine(tickOffset, p.Y, edgeOffset, p.Y, axisColor);

        var textY = p.Y - 7.5;
        AddText(n.ToString("N" + (int)Math.Max(0, -axis.RoundingPower)), textOffset, textY, "normal", 10);
      }
    }

    private void AddGridLines(AxisInfo axis)
    {
      for (double n = axis.First; n <= axis.Last; n += axis.Delta)
      {
        var gridLineColor = new SolidColorBrush(Color.FromRgb(225, 225, 225));
        var p = PointToCanvas(0, n, ViewRegion);
        AddLine(0, p.Y, ParentChart.AvailableWidth, p.Y, gridLineColor);
      }
    }

    void AddText(string text, double x, double y, string fontWeight, double fontSize = 12)
    {
      var block = new TextBlock {
        Text = text,
        Foreground = Brushes.Black,
        FontWeight = (FontWeight)new FontWeightConverter().ConvertFrom(fontWeight),
        FontSize = fontSize
      };
      GraphCanvas.Children.Add(block);
      Canvas.SetLeft(block, x);
      Canvas.SetTop(block, y);
    }

    static void SetLineStyle(Shape shape, LineStyle lineStyle)
    {
      if (lineStyle == LineStyle.Dashed)
      {
        shape.StrokeDashArray = DoubleCollection.Parse("8,5");
        shape.StrokeDashCap = PenLineCap.Round;
      }
    }

    void AddLine(double x1, double y1, double x2, double y2, Brush stroke, LineStyle lineStyle = LineStyle.Solid, double thickness = 1)
    {
      var path = new Path();
      path.Stroke = stroke;
      path.StrokeThickness = thickness;
      SetLineStyle(path, lineStyle);

      var geom = new StreamGeometry();
      using (var ctx = geom.Open())
      {
        ctx.BeginFigure(new Point(x1, y1), false, false);
        ctx.LineTo(new Point(x2, y2), true, true);
      }
      geom.Freeze();
      path.Data = geom;
      RenderOptions.SetEdgeMode(path, EdgeMode.Aliased);
      GraphCanvas.Children.Add(path);
    }

    void AddLine(double x1, double y1, double x2, double y2, Brush stroke, LineStyle lineStyle, double thickness, Rect region)
    {
      var p1 = PointToCanvas(x1, y1, region);
      var p2 = PointToCanvas(x2, y2, region);
      AddLine(p1.X, p1.Y, p2.X, p2.Y, stroke, lineStyle, thickness);
    }

    void AddRectangle(double left, double top, double right, double bottom, Brush fill)
    {
      var path = new Path();
      path.Fill = fill;
      path.Stroke = fill;
      path.StrokeThickness = 1;

      var geom = new StreamGeometry();
      using (var ctx = geom.Open())
      {
        ctx.BeginFigure(new Point(left, top), true, true);
        ctx.LineTo(new Point(right, top), true, false);
        ctx.LineTo(new Point(right, bottom), true, false);
        ctx.LineTo(new Point(left, bottom), true, false);
      }
      geom.Freeze();
      path.Data = geom;
      RenderOptions.SetEdgeMode(path, EdgeMode.Aliased);
      GraphCanvas.Children.Add(path);
    }


    int InternalPadding = 0;

    Point PointToCanvas(double x, double y, Rect region)
    {
      return new Point((x - region.Left) / region.Width * (ParentChart.AvailableWidth - 2 * InternalPadding) + InternalPadding,
        (1 - (y - region.Top) / region.Height) * (AvailableHeight - 2 * InternalPadding) + InternalPadding);
    }

    Point PointFromCanvas(double x, double y, Rect region)
    {
      return new Point((x - InternalPadding) / (ParentChart.AvailableWidth - 2 * InternalPadding) * region.Width + region.Left,
        (1 - (y - InternalPadding) / (AvailableHeight - 2 * InternalPadding)) * region.Height + region.Top);
    }
  }

  public enum PeriodType { OneDay }

  public enum PlotType { Candlestick, ValueLine }

  public enum LineStyle { Solid, Dashed }

  public class Plot
  {
    public string Title { get; set; }
    public PlotType Type { get; set; }
    public Brush Color { get; set; }
    public LineStyle LineStyle { get; set; }
    public DataSeries DataSeries { get; set; }
  }
}
