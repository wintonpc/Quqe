using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machine.Specifications;
using Quqe.NewVersace;
using Quqe;

namespace QuqeTest
{
  [TestFixture]
  class AccountTests
  {
    [Test]
    public void Initialization()
    {
      var a = new VAccount(10000, 4, 3);
      a.Equity.ShouldEqual(10000);
      a.Invested.ShouldEqual(0);
      a.TradeCost.ShouldEqual(3);
      a.MarginFactor.ShouldEqual(4);
      a.BuyingPower.ShouldEqual(40000);
    }

    [Test]
    public void LongPosition()
    {
      var a = new VAccount(10000, 4, 3);
      a.Buy("DIA", 10, 1.50);
      a.Equity.ShouldEqual(9997);
      a.Invested.ShouldEqual(15);
      a.BuyingPower.ShouldEqual(39973);

      a.Sell("DIA", 10, 2.50);
      a.Equity.ShouldEqual(10004);
      a.Invested.ShouldEqual(0);
      a.BuyingPower.ShouldEqual(40016);
    }

    [Test]
    public void MultipleBuys()
    {
      var a = new VAccount(10000, 4, 3);
      a.Buy("DIA", 10, 1.00);
      a.Buy("DIA", 10, 2.00);
      a.Sell("DIA", 20, 2.50);
      a.Equity.ShouldEqual(10011);
    }

    [Test]
    public void MultipleSells()
    {
      var a = new VAccount(10000, 4, 3);
      a.Buy("DIA", 20, 1.00);
      a.Sell("DIA", 10, 2.00);
      a.Equity.ShouldEqual(10004);
      a.Sell("DIA", 10, 3.00);
      a.Equity.ShouldEqual(10021);
    }

    [Test]
    public void JustEnoughCashToBuy()
    {
      var a = new VAccount(10, 4, 3);
      a.Buy("DIA", 4, 7.00);
    }

    [Test]
    public void NotEnoughCashToBuy()
    {
      var a = new VAccount(10, 4, 3);
      new Action(() => a.Buy("DIA", 4, 7.01)).ShouldThrow<Exception>(x => x.Message.ShouldEqual("Insufficient buying power"));
    }

    [Test]
    public void CannotSellMoreThanYouHave()
    {
      var a = new VAccount(10, 4, 3);
      a.Buy("DIA", 4, 7.00);
      new Action(() => a.Sell("DIA", 5, 7.00)).ShouldThrow<Exception>(x => x.Message.ShouldContain("Can't sell"));
    }
  }
}
