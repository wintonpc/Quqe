using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe.Rabbit
{
  public abstract class HostInfo
  {
    public readonly string Hostname;
    public readonly string Username;
    public readonly string Password;

    protected HostInfo(string hostname, string username, string password)
    {
      Hostname = hostname;
      Username = username;
      Password = password;
    }
  }

  public class RabbitHostInfo : HostInfo
  {
    public RabbitHostInfo(string hostname)
      : base(hostname, "guest", "guest")
    {
    }

    public RabbitHostInfo(string hostname, string username, string password)
      : base(hostname, username, password)
    {
    }

    public static RabbitHostInfo FromAppSettings()
    {
      var conf = ConfigurationManager.AppSettings;
      return new RabbitHostInfo(conf["RabbitHost"], conf["Username"], conf["Password"]);
    }
  }

  public class MongoHostInfo : HostInfo
  {
    public readonly string DatabaseName;

    public MongoHostInfo(string connectionString)
      : base(connectionString, null, null)
    {
      DatabaseName = "versace";
    }

    public MongoHostInfo(string connectionString, string username, string password, string dbName)
      : base(connectionString, username, password)
    {
      DatabaseName = dbName;
    }

    public static MongoHostInfo FromAppSettings()
    {
      var conf = ConfigurationManager.AppSettings;
      return new MongoHostInfo(conf["MongoHost"], conf["Username"], conf["Password"], conf["DatabaseName"]);
    }

    public static MongoHostInfo Local()
    {
      var conf = ConfigurationManager.AppSettings;
      return new MongoHostInfo(conf["LocalMongoHost"], conf["Username"], conf["Password"], conf["DatabaseName"]);
    }
  }
}