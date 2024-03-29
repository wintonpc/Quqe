﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Quqe;

namespace QuqeViz
{
  /// <summary>
  /// Interaction logic for EqPlot.xaml
  /// </summary>
  public partial class EqPlot : UserControl
  {
    Bmp TheBitmap;
    new int Width = 1024;
    new int Height = 512;
    public EqPlot()
    {
      InitializeComponent();
      TheBitmap = new Bmp(Width, Height);
      TheImage.Source = TheBitmap.B;
    }

    public Rect Bounds { get; set; }

    public void DrawPixels(int width, int height, Func<int, int, Color> f)
    {
      TheBitmap.Lock();
      for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
          TheBitmap.SetPixel(x, y, f(x, y));
      TheBitmap.Unlock();
    }

    public void DrawSurface(Func<double, double, double> f, DrawMode drawMode)
    {
      TheBitmap.Lock();
      for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
          var p = PixelToPoint(x, y);
          var z = f(p.X, p.Y);
          byte v;
          if (drawMode == DrawMode.Contour)
            v = (byte)((int)(z * 3) % 15 == 0 ? 0 : 255);
          else
            v = (byte)Math.Min(z * 3, 255);
          var c = Color.FromRgb(v, v, v);
          TheBitmap.SetPixel(x, y, c);
        }
      TheBitmap.Unlock();
    }

    public void DrawLineGraph(List<double> ys, Color color)
    {
      Bounds = new Rect(0, ys.Min(), ys.Count, ys.Max() - ys.Min());
      DrawLine(Lists.Repeat(ys.Count, x => new Point(x, ys[x])), color);
    }

    public void DrawLineGraph(List<PlotDesc> pds)
    {
      var minAll = pds.SelectMany(p => p.ys).Min();
      var maxAll = pds.SelectMany(p => p.ys).Max();
      Bounds = new Rect(0, minAll, pds.Max(p => p.ys.Count), maxAll);
      foreach (var p in pds)
      {
        var min = p.ys.Min();
        var max = p.ys.Max();
        var scaled = p.ys.Select(y => ((y - min) / (max - min) * (maxAll - minAll)) + minAll).ToList();
        DrawLine(Lists.Repeat(scaled.Count, x => new Point(x, scaled[x])), p.Color);
      }
    }

    public void DrawLine(IEnumerable<Point> points, Color color)
    {
      var ps = points.Select(PointToPixel).ToList();
      for (int i = 1; i < ps.Count; i++)
        TheBitmap.B.DrawLine((int)ps[i - 1].X, (int)ps[i - 1].Y, (int)ps[i].X, (int)ps[i].Y, color);
    }

    public void Clear(Color color)
    {
      TheBitmap.B.Clear(color);
    }

    public Point PixelToPoint(int x, int y)
    {
      return new Point {
        X = (double)x / Width * Bounds.Width + Bounds.X,
        Y = (1 - (double)y / Height) * Bounds.Height + Bounds.Y
      };
    }

    public Point PointToPixel(Point p)
    {
      return new Point {
        X = (p.X - Bounds.X) / Bounds.Width * Width,
        Y = (1 - ((p.Y - Bounds.Y) / Bounds.Height)) * Height
      };
    }
  }

  public enum DrawMode { Gradient, Contour }

  public class PlotDesc
  {
    public Color Color;
    public List<double> ys;
  }

  public class Bmp
  {
    public WriteableBitmap B { get; private set; }
    int Width;
    int Height;
    public Bmp(int width, int height)
    {
      Width = width;
      Height = height;
      B = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
    }

    public void Lock()
    {
      B.Lock();
    }

    public void Unlock()
    {
      B.AddDirtyRect(new Int32Rect(0, 0, Width, Height));
      B.Unlock();
    }

    public void SetPixel(int x, int y, Color c)
    {
      unsafe
      {
        int addr = (int)B.BackBuffer + y * B.BackBufferStride + x * 4;
        int color_data = 255 << 24 | c.R << 16 | c.G << 8 | c.B;
        *((int*)addr) = color_data;
      }
    }
  }
}
