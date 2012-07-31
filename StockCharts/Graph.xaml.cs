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
      Trades = new ObservableCollection<TradeRecord>();
      InitializeComponent();
      this.DataContext = this;
      Loaded += delegate { RedrawNeeded(); };
      SizeChanged += delegate { RedrawNeeded(); };
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

    public void AddTrades(IEnumerable<TradeRecord> trades)
    {
      foreach (var t in trades)
        Trades.Add(t);
      RedrawNeeded();
    }

    public string Title { get; set; }
    public ObservableCollection<Plot> Plots { get; set; }
    public ObservableCollection<TradeRecord> Trades { get; set; }
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
          var el = ds[i - pb.Left];
          if (!double.IsNaN(el.Min))
            minVal = Math.Min(minVal, el.Min);
          if (!double.IsNaN(el.Max))
            maxVal = Math.Max(maxVal, el.Max);
          if (!double.IsNaN(el.Min) || !double.IsNaN(el.Max))
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
            path.StrokeThickness = Math.Max(1, p.LineThickness);
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
          else if (p.Type == PlotType.Bar || p.Type == PlotType.Dash || p.Type == PlotType.Dot)
          {
            var vs = ((DataSeries<Value>)p.DataSeries).ToArray();
            for (int i = pb.ClipLeft; i <= pb.ClipRight; i++)
            {
              int k = i - pb.Left;

              var val = vs[k].Val;
              if (double.IsNaN(val))
                continue;

              var p1 = PointToCanvas(i - SlotOffset, val, ViewRegion);
              if (p.Type == PlotType.Bar)
              {
                var p2 = PointToCanvas(i - SlotOffset, 0, ViewRegion);
                AddRectangle(
                  p2.X - Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                  p2.Y,
                  p1.X + Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                  p1.Y,
                  p.Color);
              }
              else if (p.Type == PlotType.Dash)
              {
                AddRectangle(
                  p1.X - Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                  p1.Y,
                  p1.X + Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                  p1.Y + 1,
                  p.Color);
              }
              else if (p.Type == PlotType.Dot)
              {
                AddCircle(p1.X, p1.Y, (ParentChart.SlotPxWidth - 3) / 2, p.Color);
              }
            }
          }
          else if (p.Type == PlotType.Candlestick)
          {
            var bars = ((DataSeries<Bar>)p.DataSeries).ToArray();
            for (int i = pb.ClipLeft; i <= pb.ClipRight; i++)
            {
              int k = i - pb.Left;
              var bar = bars[k];
              Brush brush;
              if (p.CandleColors == CandleColors.WhiteBlack)
                brush = Brushes.Black;
              else
                brush = bar.Close < bar.Open ? CandleRed : CandleGreen;
              AddLine(i - SlotOffset, bar.High, i - SlotOffset, bar.Low, brush, LineStyle.Solid, 1, ViewRegion);
              var p1 = PointToCanvas(i - SlotOffset, bar.Open, ViewRegion);
              var p2 = PointToCanvas(i - SlotOffset, bar.Close, ViewRegion);
              AddRectangle(
                p1.X - Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                p1.Y,
                p2.X + Math.Floor((ParentChart.SlotPxWidth - 3) / 2),
                p2.Y,
                brush, (p.CandleColors == CandleColors.WhiteBlack && bar.IsGreen) ? Brushes.White : null);
            }
          }
        }

        AddVerticalAxis(axisInfo);
      }

      // time axis
      if (DrawTimeAxis)
        AddTimeAxis();

      double yOffset = 0;
      if (!string.IsNullOrEmpty(Title))
      {
        AddText(Title, 2, 0, "bold");
        yOffset += 16;
      }
      foreach (var p in Plots.Where(x => x.Type != PlotType.Candlestick && !string.IsNullOrEmpty(x.Title)))
      {
        AddText(p.Title, 16, yOffset, "normal", Brushes.Black, Brushes.GhostWhite);
        double thickness = p.Type == PlotType.ValueLine ? 2 : 6;
        AddLine(2, yOffset + 9, 14, yOffset + 9, p.Color, LineStyle.Solid, thickness);
        yOffset += 16;
      }
    }

    Brush CandleRed = Brushes.Red;
    Brush CandleGreen = new SolidColorBrush(Color.FromRgb(25, 210, 0));


    public void OnHoverStarted(Point canvasPoint)
    {
      OnHoverEnded();

      var slotNumber = GetSlotNumber(canvasPoint);
      if (slotNumber < 0 || slotNumber >= ParentChart.SlotsInView) // stray point?
        return;

      DrawCrosshairs(canvasPoint, slotNumber);
      DrawEntryAndExit(canvasPoint, slotNumber);
    }


    Path EntryArrow;
    Path ExitArrow;
    private void DrawEntryAndExit(Point canvasPoint, int slotNumber)
    {
      if (!(0 <= canvasPoint.Y && canvasPoint.Y <= AvailableHeight))
        return;

      var barsPlot = Plots.FirstOrDefault(p => p.Type == PlotType.Candlestick);
      if (barsPlot == null)
        return;

      var pb = PlotBoundaries[barsPlot];

      var i = SlotOffset + slotNumber - pb.Left;
      if (i < 0)
        return;
      var bar = ((DataSeries<Bar>)barsPlot.DataSeries)[i];
      var trade = Trades.FirstOrDefault(t => t.EntryTime.Date == bar.Timestamp);
      if (trade == null)
        return;

      var p1 = PointToCanvas(0, trade.Entry, ViewRegion);
      var xBase = slotNumber * ParentChart.SlotPxWidth;

      var entryColor = trade.PositionDirection == PositionDirection.Long ? CandleGreen : CandleRed;
      EntryArrow = AddPolygon(Brushes.Black, 1, entryColor,
        new Point(xBase, p1.Y),
        new Point(xBase - 8, p1.Y - 8),
        new Point(xBase - 8, p1.Y + 8));

      var p2 = PointToCanvas(0, trade.Exit, ViewRegion);
      xBase += ParentChart.SlotPxWidth;
      var exitColor = trade.PositionDirection == PositionDirection.Short ? CandleGreen : CandleRed;
      ExitArrow = AddPolygon(Brushes.Black, 1, exitColor,
        new Point(xBase, p2.Y),
        new Point(xBase + 8, p2.Y - 8),
        new Point(xBase + 8, p2.Y + 8));
    }

    private void HideEntryAndExit()
    {
      if (EntryArrow != null)
      {
        GraphCanvas.Children.Remove(EntryArrow);
        EntryArrow = null;
      }
      if (ExitArrow != null)
      {
        GraphCanvas.Children.Remove(ExitArrow);
        ExitArrow = null;
      }
    }

    public void OnHoverEnded()
    {
      HideCrosshairs();
      HideEntryAndExit();
    }

    int GetSlotNumber(Point canvasPoint)
    {
      var p = PointFromCanvas(canvasPoint.X, canvasPoint.Y, ViewRegion);
      return (int)Math.Round(p.X);
    }

    Line CrosshairLineV;
    Line CrosshairLineH;
    public void DrawCrosshairs(Point canvasPoint, int slotNumber)
    {
      //var lineColor = new SolidColorBrush(Color.FromRgb(173, 40, 230));
      var lineColor = new SolidColorBrush(Color.FromRgb(80, 175, 185));

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

    public void HideCrosshairs()
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
      var targetAxisPxSpacing = (int)(7 * Math.Pow(Math.Log10(GraphCanvas.ActualHeight + 1), 1.8) - 5);
      int numTicks = (int)(AvailableHeight / targetAxisPxSpacing);
      var rawDelta = range / numTicks;

      var p = (int)Math.Floor(Math.Log10(rawDelta));
      var roundDelta = RoundToPowerOfTen(rawDelta, p, Math.Round);
      var error = range % roundDelta;
      var roundFirst = RoundToPowerOfTen(minVal + error / 2, p, Math.Ceiling);

      double zeroOffset = 0;
      if (roundFirst < 0 && maxVal > 0)
        zeroOffset = Math.Round(roundFirst / roundDelta) * roundDelta - roundFirst;

      var first = roundFirst + zeroOffset;
      var last = maxVal + zeroOffset;

      if (first < minVal)
        first += roundDelta;
      if (last > maxVal)
        last -= roundDelta;

      return new AxisInfo {
        First = first,
        Last = last,
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
        var weight = n == 0 ? "bold" : "normal";
        AddText(Humanize(n, axis.RoundingPower), textOffset, textY, weight, 10);
      }
    }

    static string Humanize(double n, int roundingPower)
    {
      if (n < 1000)
        return n.ToString("N" + (int)Math.Max(0, -roundingPower));

      var suffixes = new[] { "K", "M", "B", "T" };

      for (int z = 1; z <= 4; z++)
        if (n < Math.Pow(10, 3 * (z + 1)))
          return (n / Math.Pow(10, 3 * z)).ToString("N" + Math.Max(0, 3 * z - roundingPower).ToString()) + suffixes[z - 1];

      return n.ToString();
    }

    private void AddGridLines(AxisInfo axis)
    {
      for (double n = axis.First; n <= axis.Last; n += axis.Delta)
      {
        byte lightness = (byte)(n == 0 ? 150 : 240);
        var gridLineColor = new SolidColorBrush(Color.FromRgb(lightness, lightness, lightness));
        var p = PointToCanvas(0, n, ViewRegion);
        AddLine(0, p.Y, ParentChart.AvailableWidth, p.Y, gridLineColor);
      }
    }

    void AddText(string text, double x, double y, string fontWeight, double fontSize = 12)
    { AddText(text, x, y, fontWeight, Brushes.Black, null, fontSize); }

    void AddText(string text, double x, double y, string fontWeight, Brush brush, Brush outlineColor, double fontSize = 12)
    {
      if (outlineColor != null)
      {
        for (int xo = -1; xo <= 1; xo++)
          for (int yo = -1; yo <= 1; yo++)
            AddText(text, x + xo, y + yo, fontWeight, outlineColor, null, fontSize);
      }

      var block = new TextBlock {
        Text = text,
        Foreground = brush,
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

    void AddRectangle(double left, double top, double right, double bottom, Brush stroke, Brush fill = null)
    {
      var path = new Path();
      path.Fill = fill ?? stroke;
      path.Stroke = stroke;
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

    void AddCircle(double x, double y, double radius, Brush stroke, Brush fill = null)
    {
      var path = new Ellipse();
      path.Fill = fill ?? stroke;
      path.Stroke = stroke;
      path.StrokeThickness = 1;
      path.Width = radius * 2;
      path.Height = radius * 2;
      GraphCanvas.Children.Add(path);
      Canvas.SetLeft(path, x - radius);
      Canvas.SetTop(path, y - radius);
    }

    Path AddPolygon(Brush stroke, double strokeThickness, Brush fill, params Point[] points)
    {
      var path = new Path();
      path.Stroke = stroke;
      path.Fill = fill;
      path.StrokeThickness = strokeThickness;

      var geom = new StreamGeometry();
      using (var ctx = geom.Open())
      {
        ctx.BeginFigure(points.First(), true, true);
        foreach (var p in points.Skip(1))
          ctx.LineTo(p, true, false);
      }
      geom.Freeze();
      path.Data = geom;
      GraphCanvas.Children.Add(path);
      return path;
    }


    int InternalPadding = 3;

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

  public enum PlotType { Candlestick, ValueLine, Bar, Dash, Dot }

  public enum CandleColors { GreenRed, WhiteBlack }

  public enum LineStyle { Solid, Dashed }

  public class Plot
  {
    public string Title { get; set; }
    public PlotType Type { get; set; }
    public Brush Color { get; set; }
    public LineStyle LineStyle { get; set; }
    public double LineThickness { get; set; }
    public DataSeries DataSeries { get; set; }
    public CandleColors CandleColors { get; set; }
  }
}
