using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MM.Test
{
    [TestFixture]
    public class TestMarketMaker
    {
        private MarketMaker maker;

        [SetUp]
        public void setup()
        {
            maker = new MarketMaker(new SignalGenerator(100, 2, 0), new NullLogger(), new SimulationParameters() { MAX_WAIT_PERIOD_MS = 2 }, new MatchingEngine(new NullLogger()));
        }

        [TearDown]
        public void teardown()
        {
            maker = null;
        }

        [Test]
        public void TestLongPositionOneFill()
        {
            maker.FillCallback(new Fill(100, 1000, new Order(TradeType.buy, 100, 1000)));
            maker.CalculatePosition(out PositionPair longPosition, out PositionPair shortPosition);

            Assert.That(longPosition.Amount, Is.EqualTo(1000));
            Assert.That(longPosition.AveragePrice, Is.EqualTo(100));
            Assert.That(shortPosition.Amount, Is.EqualTo(0));
            Assert.That(shortPosition.AveragePrice, Is.EqualTo(0));
        }

        [Test]
        public void TestShortPositionOneFill()
        {
            maker.FillCallback(new Fill(100, 1000, new Order(TradeType.sell, 100, 1000)));
            maker.CalculatePosition(out PositionPair longPosition, out PositionPair shortPosition);

            Assert.That(shortPosition.Amount, Is.EqualTo(1000));
            Assert.That(shortPosition.AveragePrice, Is.EqualTo(100));
            Assert.That(longPosition.Amount, Is.EqualTo(0));
            Assert.That(longPosition.AveragePrice, Is.EqualTo(0));
        }


    }
}
