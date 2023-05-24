using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Collections;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Threading;

namespace MM
{

    public delegate void OrderFilledDlg(Fill fillObject);

    public enum OrderMatcherState
    {
        Closed = 0, Open = 1, OpenAuction = 2, ClosingAuction = 3
    }

    public class MatchingEngine
    {
        private OrderMatcherState _state = OrderMatcherState.Closed;
        private readonly object _lock = new object();

        Dictionary<double, List<Order>> _bidOrderBook = new Dictionary<double, List<Order>>();
        Dictionary<double, List<Order>> _askOrderBook = new Dictionary<double, List<Order>>();
        private readonly ILocalLogger logger;

        public MatchingEngine(ILocalLogger logger)
        {
            this.logger = logger;
        }

        internal Dictionary<double, List<Order>> BidOrderBook
        {
            get
            {
                return _bidOrderBook;
            }
        }

        internal Dictionary<double, List<Order>> AskOrderBook
        {
            get
            {
                return _askOrderBook;
            }
        }

        public PricePair GetBestBidOffer()
        {
            lock (_lock)
            {
                var pair = new PricePair();
                if (_bidOrderBook.Keys.Count > 0)
                {
                    foreach (var bid in _bidOrderBook.Keys.OrderByDescending(x => x))
                    {
                        pair.Bid = bid;
                        pair.BidSize = _bidOrderBook[pair.Bid.Value].Sum(order => order.remsize);
                        if (pair.BidSize > 0)
                            break;
                    }
                    if (pair.BidSize == 0)
                    {
                        pair.Bid = null;
                        pair.BidSize = null;
                    }
                }
                if (_askOrderBook.Keys.Count > 0)
                {
                    foreach (var ask in _askOrderBook.Keys.OrderBy(x => x))
                    {
                        pair.Ask = ask;
                        pair.AskSize = _askOrderBook[pair.Ask.Value].Sum(order => order.remsize);
                        if (pair.AskSize > 0)
                            break;
                    }

                    if (pair.AskSize == 0)
                    {
                        pair.Ask = null;
                        pair.AskSize = null;
                    }
                }

                return pair;
            }
        }

        public bool Cancel(Order o)
        {
            bool result = false;

            if (o == null)
            {
                logger.log("Engine,Failed to cancel, order was null");
                return false;
            }

            lock (_lock)
            {
                Dictionary<double, List<Order>> bookToCancelFrom = null;
                if (o.type == TradeType.buy)
                    bookToCancelFrom = _bidOrderBook;
                else if (o.type == TradeType.sell)
                    bookToCancelFrom = _askOrderBook;

                var matchedOrders =
                    bookToCancelFrom
                    .Where(lst => lst.Key == o.price)
                    .SelectMany(x => x.Value)
                    .Where((value) => value.Guid == o.Guid).Select((value) => value).ToList();

                if (matchedOrders != null)
                {
                    switch (matchedOrders.Count)
                    {
                        case 1:
                            if (bookToCancelFrom[o.price].Remove(o) == false)
                                logger.log($"Engine,Remove returned false. Unable to cancel {o}");
                            result = true;
                            break;
                        default:
                            if (matchedOrders.Count > 1)
                            {
                                logger.log("Engine,Duplicate order GUID detected");
                                bookToCancelFrom[o.price].RemoveAll(x => x.Guid == o.Guid);
                                result = false;
                            }
                            else if (matchedOrders.Count == 0)
                            {
                                logger.log($"Engine,Order not found to cancel {o}");
                                result = false;
                            }

                            break;
                    }
                }
            }

            return result;
        }

    
        public void Start()
        {
            _state = OrderMatcherState.Open;
        }

        public void Stop()
        {
            _state = OrderMatcherState.Closed;
            CancelAll();
        }

        public void CancelAll()
        {
            lock(_lock)
            {
                _bidOrderBook.Clear();
                _askOrderBook.Clear();
            }
        }

        private bool IsUniqueOrderGuid(Order o, Dictionary<double, List<Order>> orderBook)
        {
            IEnumerable<Order> existingOrders =
                orderBook
                .Where(lst => lst.Key == o.price)
                .SelectMany(x => x.Value)                
                .Where((value) => value.Guid == o.Guid).Select((value) => value);

            if (existingOrders != null && existingOrders.Count() > 0)
            {
                return false;
            }

            return true;
        }

        public bool SubmitOrder(Order order)
        {
            if(IsMarketOpen() == false)
                return false;

            if(CheckOrder(order) == false)
                return false;

            lock(_lock)
            {
                if (order.type == TradeType.buy)
                {
                    if (IsUniqueOrderGuid(order, _bidOrderBook) == false)
                    {
                        logger.log("Engine,Duplicate order submission attempted");
                        return false;
                    }

                    List<Order> matchingOrders = new List<Order>();
                    foreach(double priceLevel in _askOrderBook.Keys.Where(px => (px<=order.price)).OrderBy(px => px))
                        foreach(var bookOrder in _askOrderBook[priceLevel])
                            if (bookOrder.IsCancelled == false && order.remsize > 0)
                                matchingOrders.Add(bookOrder);

                    WalkOrderBook(matchingOrders, ref this._askOrderBook, ref this._bidOrderBook, order);
                }
                else if (order.type == TradeType.sell)
                {
                    if (IsUniqueOrderGuid(order, _askOrderBook) == false)
                    {
                        logger.log("Engine,Duplicate order submission attempted");
                        return false;
                    }

                    List<Order> matchingOrders = new List<Order>();
                    foreach (double priceLevel in _bidOrderBook.Keys.Where(px => (px >= order.price)).OrderByDescending(px => px))
                        foreach (var bookOrder in _bidOrderBook[priceLevel])
                            if (bookOrder.IsCancelled == false && order.remsize > 0)
                                matchingOrders.Add(bookOrder);

                    WalkOrderBook(matchingOrders, ref this._bidOrderBook, ref this._askOrderBook, order);
                }
            }

            return true;
        }

        private bool IsMarketOpen()
        {
            return (_state == OrderMatcherState.Open);
        }

        private void WalkOrderBook(List<Order> matchingOrders,ref Dictionary<double,List<Order>> oppositeSideOrderBook,ref Dictionary<double,List<Order>> sameSideOrderBook, Order customerOrder)
        {
            double amountMatched = 0.0;
            double averagePriceMatched = 0.0;
            double remCustomerAmount = customerOrder.remsize;
            double originalCustomerRemSize = remCustomerAmount; // save this value for later comparison
            foreach(Order orderBookItem in matchingOrders)
            {
                var matchingAmount = Math.Min(remCustomerAmount, orderBookItem.remsize);
                if (matchingAmount == 0)
                    continue;

                remCustomerAmount -= matchingAmount;
                amountMatched += matchingAmount;
                averagePriceMatched = (averagePriceMatched * amountMatched + (orderBookItem.price * orderBookItem.remsize)) / (amountMatched + orderBookItem.remsize);

                var dlg = orderBookItem.FillCallback;
                dlg?.Invoke(new Fill(orderBookItem.price, matchingAmount, orderBookItem));

                dlg = customerOrder.FillCallback;
                dlg?.Invoke(new Fill(orderBookItem.price, matchingAmount, customerOrder));

                if (amountMatched == originalCustomerRemSize)
                    break;
            }

            if(amountMatched < originalCustomerRemSize)
            {
                if(sameSideOrderBook.ContainsKey(customerOrder.price) == false)
                {
                    sameSideOrderBook.Add(customerOrder.price, new List<Order>() { customerOrder }); 
                }
                else
                {
                    sameSideOrderBook[customerOrder.price].Add(customerOrder);
                }

                logger.log($"Engine,{customerOrder}");
            }
        }


        private bool CheckOrder(Order order)
        {
            if (order.price > 0 && order.size > 0)
                return true;
            
            return false;
        }
    }
}

