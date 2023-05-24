using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace MM.Test
{
    [TestFixture]
    public class TestOrderMatcher
    {
        MatchingEngine book = null;

        [SetUp]
        public void setup()
        {
            book = new MatchingEngine(new NullLogger());
            book.Start();
        }

        [TearDown]
        public void teardown()
        {
            book = null;
        }

        [Test]
        public void TestBuyOrderEmptyBook()
        {
            Order order = new Order(TradeType.buy, 100,1000, null);
            book.SubmitOrder(order);

            var bbo = book.GetBestBidOffer();
            Assert.That(bbo.Bid, Is.EqualTo(100));
            Assert.That(bbo.BidSize, Is.EqualTo(1000));
        }

        [Test]
        public void TestSellOrderEmptyBook()
        {
            Order order = new Order(TradeType.sell,100,1000,null);
            book.SubmitOrder(order);

            var bbo = book.GetBestBidOffer();
            Assert.That(bbo.Ask, Is.EqualTo(100));
            Assert.That(bbo.AskSize, Is.EqualTo(1000));
        }

        [Test]
        public void TestTwoSellOrdersEmptyBook()
        {
            Order order = new Order(TradeType.sell, 100, 35, null);
            book.SubmitOrder(order);

            Order order2 = new Order(TradeType.sell, 105, 99, null);
            book.SubmitOrder(order2);

            Assert.That(book.AskOrderBook.Keys.Count, Is.EqualTo(2));
            Assert.That(order, Is.EqualTo(book.AskOrderBook[order.price].First()));
            Assert.That(order2, Is.EqualTo(book.AskOrderBook[order2.price].First()));
        }

        [Test]
        public void TestBuyMarketOrderFullyFilled()
        {
            Fill f = null;
            Order order = new Order(TradeType.sell,100,1000,null);
            book.SubmitOrder(order);

            
            Order fillingOrder = new Order(TradeType.buy,110,1000,delegate(Fill fill) { f = fill; });
            book.SubmitOrder(fillingOrder);

            if (f != null)
            {
                Assert.That(f.Amount, Is.EqualTo(fillingOrder.size));
                Assert.That(f.Price, Is.LessThanOrEqualTo(fillingOrder.price));
                Assert.That(order.Fullyfilled, Is.True);
                Assert.That(fillingOrder.Fullyfilled, Is.True);
            }
            else { Assert.Fail(); }
        }

        [Test]
        public void TestBuyMarketOrderPartialFilledLeavesResidual()
        {
            Fill f = null;
            var order = new Order(TradeType.sell,100,1000,null);
            book.SubmitOrder(order);

            var fillingOrder = new Order(TradeType.buy,100,2500,delegate(Fill fill) { f = fill; } );
            book.SubmitOrder(fillingOrder);

            Assert.That(f, Is.Not.Null);
            Assert.That(fillingOrder.size > f.Amount, Is.True);
            Assert.That(f.Price <= fillingOrder.price, Is.True);
            Assert.That(f.Amount, Is.EqualTo(1000));
            Assert.That(f.Price, Is.EqualTo(100));

            var bbo = book.GetBestBidOffer();
            Assert.That(bbo.Bid,Is.EqualTo(100));
            Assert.That(bbo.BidSize,Is.EqualTo(1500));

            Assert.That(fillingOrder.remsize, Is.EqualTo(1500));
            Assert.That(fillingOrder.price, Is.EqualTo(100));
        }

        [Test]
        public void TestSellMarketOrderPartialFilledLeavesResidual()
        {
            Fill f = null;
            var order = new Order(TradeType.buy, 100, 1000, null);
            book.SubmitOrder(order);

            var fillingOrder = new Order(TradeType.sell, 100, 2500, delegate (Fill fill) { f = fill; });
            book.SubmitOrder(fillingOrder);

            Assert.That(f, Is.Not.Null);
            Assert.That(fillingOrder.size > f.Amount, Is.True);
            Assert.That(f.Price <= fillingOrder.price, Is.True);
            Assert.That(f.Amount, Is.EqualTo(1000));
            Assert.That(f.Price, Is.EqualTo(100));

            var bbo = book.GetBestBidOffer();
            Assert.That(bbo.Ask, Is.EqualTo(100));
            Assert.That(bbo.AskSize, Is.EqualTo(1500));

            Assert.That(fillingOrder.remsize, Is.EqualTo(1500));
            Assert.That(fillingOrder.price, Is.EqualTo(100));
        }

        [Test]
        public void TestOrderRejectedWhenMarketClosed()
        {
            Order order = new Order(TradeType.sell, 100, 1000, null);
            book.Stop();
            Assert.That(book.SubmitOrder(order), Is.False);
        }

        [Test]
        public void TestBuyMarketOrderFillTwoOrdersAveragePrice()
        {
            Order order = new Order(TradeType.sell,100,1000,null);
            book.SubmitOrder(order);
            Order order2 = new Order(TradeType.sell,110,350,null);
            book.SubmitOrder(order2);

            var fills = new List<Fill>();
            Order fillingOrder = new Order(TradeType.buy, 110, 1350, delegate(Fill fill) { fills.Add(fill); });
            book.SubmitOrder(fillingOrder);

            double calculatedAveragePrice = (double)(100 * 1000 + 110 * 350) / (double)(1000 + 350);

            double amountFilled = 0;
            double valueFilled = 0.0;
            foreach (Fill fill in fills) 
            { 
                amountFilled += fill.Amount; 
                valueFilled += (fill.Amount * fill.Price); 
            }
            
            double averagePriceFilled = valueFilled / amountFilled;

            Assert.That(fillingOrder.size,Is.EqualTo(amountFilled));
            Assert.That(Math.Round(averagePriceFilled, 4), Is.EqualTo(Math.Round(calculatedAveragePrice,4)));
        }

        [Test]
        public void TestFillIssuesTwoCallbacks()
        {
            string callback = string.Empty;
            Order order = new Order(TradeType.sell,100,1000,delegate(Fill fillObject)
            {
                callback = "received";
            });
            book.SubmitOrder(order);

            string callback2 = string.Empty;
            Order fillingOrder = new Order(TradeType.buy,110,1350,delegate(Fill fillObject2)
            {
                callback2 = "received2";
            });
            book.SubmitOrder(fillingOrder);

            Assert.That(callback, Is.EqualTo("received"));
            Assert.That(callback2, Is.EqualTo("received2"));
        }

        [Test]
        public void TestCancelSellOrder()
        {
            Order order = new Order(TradeType.sell, 100, 1000, null);
            book.SubmitOrder(order);
            bool result = book.Cancel(order);
            Assert.IsTrue(result);
            var bbo = book.GetBestBidOffer();
            Assert.IsTrue(bbo.AskSize== null || bbo.AskSize == 0);            
        }

        [Test]
        public void TestCancelBuyOrder()
        {
            Order order = new Order(TradeType.buy, 100, 1000, null);
            book.SubmitOrder(order);
            bool result = book.Cancel(order);
            Assert.IsTrue(result);
            var bbo = book.GetBestBidOffer();
            Assert.IsTrue(bbo.BidSize == null || bbo.BidSize == 0);
        }

        [Test]
        public void TestCancelSellOrderWithBuyAlsoPresent()
        {
            Order order = new Order(TradeType.sell, 100, 1000, null);
            book.SubmitOrder(order);

            Order order2 = new Order(TradeType.buy, 95, 500, null);
            book.SubmitOrder(order2);

            bool result = book.Cancel(order);
            Assert.IsTrue(result);

            // Console.WriteLine(maker.GetOrderBook());
            Assert.That(book.AskOrderBook[order.price].Count(), Is.EqualTo(0));
            Assert.That(book.BidOrderBook[order2.price].Count(), Is.EqualTo(1));
        }

        [Test]
        public void TestCancelBuyOrderWithSellAlsoPresent()
        {
            Order order = new Order(TradeType.sell, 100, 1000, null);
            book.SubmitOrder(order);

            Order order2 = new Order(TradeType.buy, 95, 500, null);
            book.SubmitOrder(order2);

            bool result = book.Cancel(order2);
            Assert.IsTrue(result);

            Assert.That(book.AskOrderBook[order.price].Count(), Is.EqualTo(1));
            Assert.That(book.BidOrderBook[order2.price].Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestCancelSellOrderWithOtherSellsPresent()
        {
            Order order = new Order(TradeType.sell, 100, 1000, null);
            book.SubmitOrder(order);

            Order order2 = new Order(TradeType.sell, 102, 253, null);
            book.SubmitOrder(order2);

            Order order3 = new Order(TradeType.sell, 104, 339, null);
            book.SubmitOrder(order3);

            bool result = book.Cancel(order2);
            Assert.IsTrue(result);
        }

        [Test]
        public void TestFillCallbackAddsFillToList()
        {
            Fill f;
            string callback = string.Empty;
            Order order = new Order(TradeType.sell,100,1000);
            book.SubmitOrder(order);

            Order fillingOrder = new Order(TradeType.buy,110,1000,delegate(Fill fill) { f = fill;  });
            book.SubmitOrder(fillingOrder);

            Assert.That(order.Fills.Count, Is.EqualTo(1));

            Assert.IsTrue(order.Fills[0].Amount == 1000 && order.Fills[0].Price == 100); 
        }

        [Test]
        public void TestDisorderedOrdersAreFilledInPriceOrderSells()
        {
            Order order = new Order(TradeType.sell, 105, 1000, null);
            book.SubmitOrder(order);

            Order order2 = new Order(TradeType.sell, 102, 253, null);
            book.SubmitOrder(order2);

            Order order3 = new Order(TradeType.sell, 108, 339, null);
            book.SubmitOrder(order3);

            Order order4 = new Order(TradeType.sell, 99, 222, null);
            book.SubmitOrder(order4);

            Fill fill = null;
            Order fillingOrder = new Order(TradeType.buy, 108, 222, delegate(Fill f) { fill = f; });  //  place order at 108, expect to get filled at 99          
            book.SubmitOrder(fillingOrder);
            Assert.True(fill != null && fill.Amount == 222);
            Assert.That(fill != null && fill.Price == (double)99.0);
        }
        [Test]
        public void TestDisorderedOrdersAreFilledInPriceOrderBuys()
        {
            Order order = new Order(TradeType.buy, 105, 1000, null);
            book.SubmitOrder(order);

            Order order2 = new Order(TradeType.buy, 102, 253, null);
            book.SubmitOrder(order2);

            Order order3 = new Order(TradeType.buy, 108, 339, null);
            book.SubmitOrder(order3);

            Order order4 = new Order(TradeType.buy, 99, 222, null);
            book.SubmitOrder(order4);

            Fill f4 = null;
            Order fillingOrder = new Order(TradeType.sell, 106, 1000, delegate(Fill f) { f4 = f; } );  //  place order at 106, expect to get filled at 108
            book.SubmitOrder(fillingOrder);
            Assert.True(f4 != null && f4.Amount == 339);
            Assert.True(f4!= null && f4.Price == (double)108.0);
        }

        [Test]
        public void TestTwoWayOrderBook()
        {

            book.SubmitOrder(new Order(TradeType.buy, 98.2, 515));
            book.SubmitOrder(new Order(TradeType.buy, 97.6, 250));
            book.SubmitOrder(new Order(TradeType.buy, 97.6, 310));
            book.SubmitOrder(new Order(TradeType.buy, 96.1, 800));
            book.SubmitOrder(new Order(TradeType.buy, 95.4, 333));


            book.SubmitOrder( new Order(TradeType.sell, 101.9, 222));
            book.SubmitOrder( new Order(TradeType.sell, 102.8, 150));
            book.SubmitOrder(new Order(TradeType.sell, 99.2, 1000));
            book.SubmitOrder( new Order(TradeType.sell, 103.4, 200));
            book.SubmitOrder(new Order(TradeType.sell, 105, 50000));
            book.SubmitOrder(new Order(TradeType.sell, 103.4, 650));

            Assert.That(book.BidOrderBook.Count, Is.EqualTo(4));
            Assert.That(book.AskOrderBook.Count, Is.EqualTo(5));

            Assert.IsNull(null);
       }


        [Test]
        public void TestFullyFillIsTrue()
        {
            string callback = string.Empty;
            Order order = new Order(TradeType.sell,100,1000);
            book.SubmitOrder(order);

            Order fillingOrder = new Order(TradeType.buy,110,1000,null);
            book.SubmitOrder(fillingOrder);

            Assert.IsTrue(order.Fullyfilled);
        }

        [Test]
        public void TestTwoFillsMarkFullyFilled()
        {
            book.SubmitOrder(new Order(TradeType.sell,100,1000));

            Order orderToTest = new Order(TradeType.buy,110,1350);

            book.SubmitOrder(orderToTest);
            
            Assert.IsFalse(orderToTest.Fullyfilled);

            book.SubmitOrder(new Order(TradeType.sell,110,350));

            Assert.IsTrue(orderToTest.Fullyfilled);
        }

        [Test]
        public void TestCancelRemovesOrder()
        {
            Order order = new Order(TradeType.buy, 100.12, 512);
            book.SubmitOrder(order);
            var bbo = book.GetBestBidOffer();
            Assert.That(bbo.Bid, Is.EqualTo(100.12));
            Assert.That(bbo.BidSize, Is.EqualTo(512));
            Assert.IsTrue(book.Cancel(order));
            bbo = book.GetBestBidOffer();
            Assert.That(bbo.Bid, Is.Null);
            Assert.That(bbo.BidSize, Is.Null);
        }

        [Test]
        public void TestEmptyBookGivesNullBBO()
        {
            var bbo = book.GetBestBidOffer();
            Assert.That(bbo.Bid, Is.Null);
            Assert.That(bbo.BidSize, Is.Null);
            Assert.That(bbo.Ask, Is.Null);
            Assert.That(bbo.AskSize, Is.Null);
        }

        [Test]
        public void TestDuplicateOrderIsRejected()
        {
            Order order = new Order(TradeType.buy, 100.12, 512);
            Assert.That(book.SubmitOrder(order), Is.True);
            Assert.That(book.SubmitOrder(order), Is.False);
        }
    }
}
