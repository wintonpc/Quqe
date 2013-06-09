using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Quqe.Rabbit;

namespace Quqe.Rabbit
{
  public static class RabbitMessageReader
  {
    static RabbitMessageReader()
    {
      EnsureMessagesAreRegistered();
    }

    public static RabbitMessage Read(ulong deliveryTag, byte[] bs)
    {
      try
      {
        var json = Encoding.UTF8.GetString(bs);
        var bsonDoc = BsonDocument.Parse(json);
        var type = bsonDoc["Type"];
        bsonDoc.Remove("Type");
        bsonDoc.Add("_t", type);
        var msg = BsonSerializer.Deserialize<RabbitMessage>(bsonDoc);
        msg.DeliveryTag = deliveryTag;
        return msg;
      }
      catch (FileFormatException x)
      {
        return Fail(bs, deliveryTag, x);
      }
      catch (BsonSerializationException x)
      {
        return Fail(bs, deliveryTag, x);
      }
      catch (KeyNotFoundException x)
      {
        return Fail(bs, deliveryTag, x);
      }
    }

    static RabbitMessage Fail(byte[] bs, ulong deliveryTag, Exception x)
    {
      return new UnknownRequest(deliveryTag, x + "\nThe message was:\n" + Encoding.UTF8.GetString(bs));
    }

    static void EnsureMessagesAreRegistered()
    {
      foreach (var t in GetMessageTypes())
        FormatterServices.GetUninitializedObject(t).ToBson();
    }

    static IEnumerable<Type> GetMessageTypes()
    {
      var asm = typeof(RabbitMessage).Assembly;
      return asm.GetTypes().Where(t => !t.IsAbstract && typeof(RabbitMessage).IsAssignableFrom(t));
    }
  }
}
