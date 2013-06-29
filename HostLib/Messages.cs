using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Quqe.Rabbit;

namespace Workers
{
  public enum SignalType
  {
    NextClose
  }

  public class MasterRequest : RabbitMessage
  {
    public string ProtoRunName { get; private set; }
    public string Symbol { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public double ValidationPct { get; private set; }
    public SignalType SignalType { get; private set; }

    public MasterRequest(string protoRunName, string symbol, DateTime startDate, DateTime endDate, double validationPct, SignalType signalType)
    {
      ProtoRunName = protoRunName;
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
}