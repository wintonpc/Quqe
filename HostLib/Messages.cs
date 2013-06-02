using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace HostLib
{
  public enum SignalType
  {
    NextClose
  }

  public class MasterRequest : RabbitMessage
  {
    public ObjectId ProtoRunId { get; private set; }
    public string Symbol { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public double ValidationPct { get; private set; }
    public SignalType SignalType { get; private set; }

    public MasterRequest(ObjectId protoRunId, string symbol, DateTime startDate, DateTime endDate, double validationPct, SignalType signalType)
    {
      ProtoRunId = protoRunId;
      Symbol = symbol;
      StartDate = startDate;
      EndDate = endDate;
      ValidationPct = validationPct;
      SignalType = signalType;
    }
  }

  public class MasterUpdate : RabbitMessage
  {
    public ObjectId GenerationId { get; private set; }
    public int GenerationNumber { get; private set; }
    public double Fitness { get; private set; }

    public MasterUpdate(ObjectId generationId, int genNumber, double fitness)
    {
      GenerationId = generationId;
      GenerationNumber = genNumber;
      Fitness = fitness;
    }
  }

  public class MasterResult : RabbitMessage
  {
    public ObjectId RunId { get; private set; }

    public MasterResult(ObjectId runId) { RunId = runId; }
  }

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

    public byte[] ToUTF8() { return Encoding.UTF8.GetBytes(ToJson()); }
  }
}