﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using PCW;

namespace QuqeViz
{
  /// <summary>
  /// Interaction logic for EqPlot.xaml
  /// </summary>
  public partial class EqPlot : UserControl
  {
    Bmp TheBitmap;
    new int Width = 512;
    new int Height = 512;
    public EqPlot()
    {
      InitializeComponent();
      TheBitmap = new Bmp(Width, Height);
      TheImage.Source = TheBitmap.B;
      Bounds = new Rect(0, 0, 1.4, 1.4);
    }

    public Rect Bounds { get; set; }

    public void DrawSurface(Func<double, double, double> f, DrawMode drawMode)
    {
      TheBitmap.Lock();
      for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
          var p = PixelToPoint(x, y);
          var z = f(p.X, p.Y);
          var v = (byte)((int)(z * 200) % 15 == 0 ? 0 : 255);
          //var v = (byte)Math.Min(z * 200, 255);
          var c = Color.FromRgb(v, v, v);
          TheBitmap.SetPixel(x, y, c);
        }
      TheBitmap.Unlock();
    }

    public void DrawLine(IEnumerable<Point> points, Color color)
    {
      var ps = points.ToList();
      for (int i=1; i<ps.Count; i++)
        TheBitmap.B.DrawLine((int)ps[i - 1].X, (int)ps[i - 1].Y, (int)ps[i].X, (int)ps[i].Y, color);
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

    private void TheImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
      DrawSurface((x, y) => Math.Pow(x - 1, 2) + Math.Pow(y - 1, 2), DrawMode.Gradient);
      DrawLine(List.Create(new Point(0, 0), new Point(0.5, 0.2), new Point(1, 1))
        .Select(PointToPixel), Colors.Red);
    }

    private void TheImage_MouseMove(object sender, MouseEventArgs e)
    {
      var pixel = e.GetPosition(TheImage);
      var p = PixelToPoint((int)pixel.X, (int)pixel.Y);
      Trace.WriteLine(string.Format("{0}, {1} ==> {2:N4}, {3:N4}", pixel.X, pixel.Y, p.X, p.Y));
    }
  }

  public enum DrawMode { Gradient }

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
