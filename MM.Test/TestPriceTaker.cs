using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MM.Test
{
    internal class TestablePriceTaker : InformedPriceTaker
    {
        public TestablePriceTaker(MatchingEngine market) : base(market)
        {
        }

        public override void Log(string s)
        {           
        }

        public override void Run(object o)
        {            
        }

        public void AddFill(Fill f)
        {
            this._fills.Add(f);
        }
    }

    [TestFixture]
    internal class TestPriceTaker
    {
        private TestablePriceTaker taker;

        [Test]
        public void TestTrue()
        {
            Assert.IsTrue(true);
        }

        [SetUp]
        public void Setup()
        {
            taker = new TestablePriceTaker(new MatchingEngine(new NullLogger()));
        }

        [Test]
        public void TestMatchingSingleFillsPnLProfit()
        {
            Fill buy = new Fill(58d, 100, new Order(TradeType.buy, 58d, 100));
            taker.AddFill(buy);

            Fill sell = new Fill(60d, 100, new Order(TradeType.sell, 60, 100));
            taker.AddFill(sell);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(200));
            Assert.That(unrealisedPnl, Is.EqualTo(0));
            Assert.That(unrealisedUnits, Is.EqualTo(0));
            Assert.That(unrealizedAvgPx, Is.EqualTo(0d));
        }

        [Test]
        public void TestMatchingSingleFillsPnLLoss()
        {
            Fill buy = new Fill(60d, 100, new Order(TradeType.buy, 60d, 100));
            taker.AddFill(buy);

            Fill sell = new Fill(58d, 100, new Order(TradeType.sell, 58d, 100));
            taker.AddFill(sell);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(-200));
        }

        [Test]
        public void TestNonMatchingSingleFillsPnLProfit()
        {
            Fill buy = new Fill(58d, 150, new Order(TradeType.buy, 58d, 100));
            taker.AddFill(buy);

            Fill sell = new Fill(60d, 100, new Order(TradeType.sell, 60d, 100));
            taker.AddFill(sell);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(200));
            Assert.That(unrealisedUnits, Is.EqualTo(50));
        }

        [Test]
        public void TestNonMatchingSingleFillsPnLLoss()
        {
            Fill buy = new Fill(60d, 150, new Order(TradeType.buy, 60d, 100));
            taker.AddFill(buy);

            Fill sell = new Fill(58d, 100, new Order(TradeType.sell, 58d, 100));
            taker.AddFill(sell);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(-200));
            Assert.That(unrealisedUnits, Is.EqualTo(50));
        }

        [Test]
        public void TestMatchingMultipleFillsShortPnlProfit()
        {
            Fill buy = new Fill(58d, 100, new Order(TradeType.buy, 58d, 100));
            taker.AddFill(buy);

            Order sellOrder = new Order(TradeType.sell, 60d, 25);
            Fill sell = new Fill(60d, 25, sellOrder);
            Fill sell2 = new Fill(60d, 25, sellOrder);
            Fill sell3 = new Fill(60d, 25, sellOrder);
            Fill sell4 = new Fill(60d, 25, sellOrder);

            taker.AddFill(sell);
            taker.AddFill(sell2);
            taker.AddFill(sell3);
            taker.AddFill(sell4);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(200));
            Assert.That(unrealisedUnits, Is.EqualTo(0));
        }

        [Test]
        public void TestMatchingMultipleFillsShortPnlLoss()
        {
            Fill buy = new Fill(60d, 100, new Order(TradeType.buy, 60d, 100));
            taker.AddFill(buy);

            Order sellOrder = new Order(TradeType.sell, 58d, 25);
            Fill sell = new Fill(58d, 25, sellOrder);
            Fill sell2 = new Fill(58d, 25, sellOrder);
            Fill sell3 = new Fill(58d, 25, sellOrder);
            Fill sell4 = new Fill(58d, 25, sellOrder);

            taker.AddFill(sell);
            taker.AddFill(sell2);
            taker.AddFill(sell3);
            taker.AddFill(sell4);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(-200));
            Assert.That(unrealisedUnits, Is.EqualTo(0));
        }

        [Test]
        public void TestNonMatchingMultipleFillsShortPnlLoss()
        {
            Fill buy = new Fill(60d, 100, new Order(TradeType.buy, 60d, 100));
            taker.AddFill(buy);

            Order sellOrder = new Order(TradeType.sell, 60d, 25);
            Fill sell = new Fill(58d, 25, sellOrder);
            Fill sell2 = new Fill(58d, 25, sellOrder);
            Fill sell3 = new Fill(58d, 25, sellOrder);
            Fill sell4 = new Fill(58d, 25, sellOrder);
            Fill sell5 = new Fill(58d, 25, sellOrder);

            taker.AddFill(sell);
            taker.AddFill(sell2);
            taker.AddFill(sell3);
            taker.AddFill(sell4);
            taker.AddFill(sell5);

            var pnl = taker.CalculatePnl(120d, out double unrealisedPnl, out double unrealisedUnits, out double unrealizedAvgPx);
            Assert.That(pnl, Is.EqualTo(-200));
            Assert.That(unrealisedUnits, Is.EqualTo(25));
        }

    }
}
