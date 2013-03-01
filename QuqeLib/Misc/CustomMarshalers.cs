using System;
using System.Linq;
using System.Xml.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Generic;
using PCW;

namespace Quqe
{
  class VectorMarshaler : IXSerMarshaler
  {
    public Type Type { get { return typeof(Vector<double>); } }

    public object Write(object obj, IXSerWriteContext context)
    {
      var v = (DenseVector)obj;
      return new XElement("Vector",
        new XAttribute("Count", v.Count),
        LittleEndian.ToBase64String(v.ToArray(), typeof(double)));
    }

    public object Read(XElement e, Type type, IXSerReadContext context)
    {
      var content = e.Elements().Single();
      return new DenseVector(LittleEndian.FromBase64String(content.Value, typeof(double)).Cast<double>().ToArray());
    }
  }

  class MatrixMarshaler : IXSerMarshaler
  {
    public Type Type { get { return typeof(Matrix<double>); } }

    public object Write(object obj, IXSerWriteContext context)
    {
      var m = (DenseMatrix)obj;
      return new XElement("Matrix",
        new XAttribute("RowCount", m.RowCount),
        new XAttribute("ColumnCount", m.ColumnCount),
        LittleEndian.ToBase64String(m.ToColumnWiseArray(), typeof(double)));
    }

    public object Read(XElement e, Type type, IXSerReadContext context)
    {
      var content = e.Elements().Single();
      var rowCount = int.Parse(content.Attribute("RowCount").Value);
      var colCount = int.Parse(content.Attribute("ColumnCount").Value);
      return new DenseMatrix(rowCount, colCount, LittleEndian.FromBase64String(content.Value, typeof(double)).Cast<double>().ToArray());
    }
  }
}
