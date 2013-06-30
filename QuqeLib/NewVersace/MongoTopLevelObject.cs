using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[assembly: InternalsVisibleTo("QuqeTest")]

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
