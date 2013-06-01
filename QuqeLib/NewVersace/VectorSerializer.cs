using MathNet.Numerics.LinearAlgebra.Double;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCW;
using Vec = MathNet.Numerics.LinearAlgebra.Generic.Vector<double>;
using Mat = MathNet.Numerics.LinearAlgebra.Generic.Matrix<double>;

namespace Quqe.NewVersace
{
  class VectorSerializer : IBsonSerializer
  {
    public void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options)
    {
      bsonWriter.WriteBinaryData(new BsonBinaryData(LittleEndian.DoublesToBytes(((Vec)value).ToArray()), BsonBinarySubType.Binary));
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
      return new DenseVector(LittleEndian.BytesToDoubles(bsonReader.ReadBinaryData().Bytes));
    }

    public IBsonSerializationOptions GetDefaultSerializationOptions()
    {
      throw new NotImplementedException();
    }
  }
}
