using System;
using Lz4Net;
using MathNet.Numerics.LinearAlgebra.Double;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using System.Linq;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe.NewVersace
{
  public class VectorSerializer : IBsonSerializer
  {
    static object Lock = new object();
    static int NumCompressed;
    public static double AverageCompressionRatio { get; private set; }

    static void RecordCompression(int compressed, int uncompressed)
    {
      lock (Lock)
      {
        var thisRatio = (double)compressed / uncompressed;
        AverageCompressionRatio = ((AverageCompressionRatio * NumCompressed) + thisRatio) / (double)(NumCompressed + 1);
        NumCompressed++;
      }
    }

    public void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options)
    {
      var before = LittleEndian.DoublesToBytes(((Vec)value).ToArray());
      var after = Lz4.CompressBytes(before, Lz4Mode.HighCompression);
      bsonWriter.WriteBinaryData(new BsonBinaryData(after, BsonBinarySubType.Binary));
    }

    public object Deserialize(BsonReader bsonReader, Type nominalType, Type actualType, IBsonSerializationOptions options)
    {
      return Deserialize(bsonReader);
    }

    public object Deserialize(BsonReader bsonReader, Type nominalType, IBsonSerializationOptions options)
    {
      return Deserialize(bsonReader);
    }

    static Vec Deserialize(BsonReader bsonReader)
    {
      var bs = bsonReader.ReadBinaryData().Bytes;
      byte[] decompressed;
      try
      {
        decompressed = Lz4.DecompressBytes(bs);
      }
      catch (Exception)
      {
        decompressed = bs;
      }
      RecordCompression(bs.Length, decompressed.Length);
      return new DenseVector(LittleEndian.BytesToDoubles(decompressed));
    }

    public IBsonSerializationOptions GetDefaultSerializationOptions()
    {
      throw new NotImplementedException();
    }
  }
}
