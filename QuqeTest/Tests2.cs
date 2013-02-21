using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quqe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using PCW;
using Machine.Specifications;
using NUnit.Framework;
using System.Diagnostics;
using List = PCW.List;

namespace QuqeTest
{
  [TestFixture]
  public class Tests2
  {
    [Test]
    public void XSerPerformance()
    {
      var s = new VersaceSettings();
      var sw = new Stopwatch();
      sw.Start();
      List.Repeat(1000, () => XSer.Write(s));
      Trace.WriteLine(string.Format("{0}ms to write 1000", sw.ElapsedMilliseconds));
      var written = XSer.Write(s);
      sw.Restart();
      List.Repeat(1000, () => XSer.Read<VersaceSettings>(written));
      Trace.WriteLine(string.Format("{0}ms to read 1000", sw.ElapsedMilliseconds));
    }

    [Test]
    public void VersaceSettingsBootstrap()
    {
      var s = new VersaceSettings();
      XSer.Write(s).Save("VersaceSettings/default.xml");
    }
  }
}
