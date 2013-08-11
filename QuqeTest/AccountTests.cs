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
    public void MaxShares()
    {
      var a = new VAccount(28, 4, 3);
      a.MaxSharesAtPrice(9).ShouldEqual(11);
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
    public void ShortPosition()
    {
      var a = new VAccount(10000, 4, 3);
      a.Short("DIA", 30, 5.00);
      a.Equity.ShouldEqual(9997);
      a.Invested.ShouldEqual(150);
      a.BuyingPower.ShouldEqual(39838);

      a.Cover("DIA", 30, 4.00);
      a.Equity.ShouldEqual(10024);
      a.Invested.ShouldEqual(0);
      a.BuyingPower.ShouldEqual(40096);
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
    public void MultipleShorts()
    {
      var a = new VAccount(10000, 4, 3);
      a.Short("DIA", 10, 2.00);
      a.Short("DIA", 10, 3.00);
      a.Cover("DIA", 20, 1.50);
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
    public void MultipleCovers()
    {
      var a = new VAccount(10000, 4, 3);
      a.Short("DIA", 20, 5.00);
      a.Cover("DIA", 10, 2.00);
      a.Equity.ShouldEqual(10024);
      a.Cover("DIA", 10, 3.00);
      a.Equity.ShouldEqual(10041);
    }

    [Test]
    public void JustEnoughEquityToBuy()
    {
      var a = new VAccount(10, 4, 3);
      a.Buy("DIA", 4, 7.00);
    }

    [Test]
    public void NotEnoughEquityToBuy()
    {
      var a = new VAccount(10, 4, 3);
      new Action(() => a.Buy("DIA", 4, 7.01)).ShouldThrow<Exception>(x => x.Message.ShouldEqual("Insufficient buying power"));
    }

    [Test]
    public void JustEnoughEquityToShort()
    {
      var a = new VAccount(10, 4, 3);
      a.Short("DIA", 4, 7.00);
    }

    [Test]
    public void NotEnoughEquityToShort()
    {
      var a = new VAccount(10, 4, 3);
      new Action(() => a.Short("DIA", 4, 7.01)).ShouldThrow<Exception>(x => x.Message.ShouldEqual("Insufficient buying power"));
    }

    [Test]
    public void CannotSellMoreThanYouHave()
    {
      var a = new VAccount(10000, 4, 3);
      a.Buy("DIA", 4, 7.00);
      new Action(() => a.Sell("DIA", 5, 7.00)).ShouldThrow<Exception>(x => x.Message.ShouldContain("Can't sell"));
    }

    [Test]
    public void CannotCoverMoreThanYouShorted()
    {
      var a = new VAccount(10000, 4, 3);
      a.Short("DIA", 4, 7.00);
      new Action(() => a.Cover("DIA", 5, 7.00)).ShouldThrow<Exception>(x => x.Message.ShouldContain("Can't cover"));
    }
  }
}
