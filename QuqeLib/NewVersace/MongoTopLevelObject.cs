using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;

namespace Quqe
{
  public abstract class MongoTopLevelObject
  {
    public ObjectId Id { get; protected set; }

    [BsonIgnore]
    internal Database Database;

    protected MongoTopLevelObject(Database db)
    {
      Database = db;
    }
  }
}
