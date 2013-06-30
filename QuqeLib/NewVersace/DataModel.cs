using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quqe
{
  public class DbBar : MongoTopLevelObject
  {
    public string Symbol { get; private set; }
    public DateTime Timestamp { get; private set; }
    public double Open { get; private set; }
    public double Low { get; private set; }
    public double High { get; private set; }
    public double Close { get; private set; }
    public long Volume { get; private set; }

    public DbBar(Database db, string symbol, DateTime timestamp, double open, double low, double high, double close, long volume) : base(db)
    {
      Symbol = symbol;
      Timestamp = timestamp;
      Open = open;
      Low = low;
      High = high;
      Close = close;
      Volume = volume;
    }
  }
}
