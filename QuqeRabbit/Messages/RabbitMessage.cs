using System;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Quqe.Rabbit
{
  [Serializable]
  public abstract class RabbitMessage
  {
    ulong? _DeliveryTag;

    [BsonIgnore]
    public ulong DeliveryTag
    {
      get { return _DeliveryTag.Value; }
      set
      {
        if (_DeliveryTag.HasValue && _DeliveryTag.Value != value)
          throw new InvalidOperationException(string.Format("Cannot set DeliveryTag to {0}. It is already set to {1}",
                                                            value, _DeliveryTag.Value));
        _DeliveryTag = value;
      }
    }

    public string ToJson()
    {
      var bsonDoc = this.ToBsonDocument<object>();
      var type = bsonDoc["_t"];
      bsonDoc.Remove("_t");
      bsonDoc.InsertAt(0, new BsonElement("Type", type));
      return bsonDoc.ToJson();
    }

    public byte[] ToUtf8() { return Encoding.UTF8.GetBytes(ToJson()); }
  }

  public class ReceiveWasCancelled : RabbitMessage
  {
  }

  public class ReceiveTimedOut : RabbitMessage
  {
  }

  public class UnknownRequest : RabbitMessage
  {
    public string Error { get; private set; }
    public UnknownRequest(ulong deliveryTag, string error = "")
    {
      DeliveryTag = deliveryTag;
      Error = error;
    }
  }
}
