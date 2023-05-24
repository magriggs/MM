using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MM
{
    public class Order
    {
        private readonly Object lockObject = new object();

        public Order(TradeType buysell,double price,double size) : this(buysell, price, size, null)
        {
            this.FillCallback = new OrderFilledDlg(this.FillCallbackHandler);
        }

        private void FillCallbackHandler(Fill fillObject)
        {
            lock (lockObject)
            {
                if (this.Fills == null)
                {
                    this.Fills = new List<Fill>();
                }

                this.Fills.Add(fillObject);
                this.Fullyfilled = remsize == 0 ? true : false;
            }
        }

        public Order(TradeType buysell,double price,double size,OrderFilledDlg callback)
        {
            this.type = buysell;
            this.price = price;
            this.size = size;
            this.FillCallback = new OrderFilledDlg(this.FillCallbackHandler);
            this.FillCallback += callback;
            this.Guid = Guid.NewGuid();
        }
        public TradeType type
        {
            get;
            set;
        }
        public double price
        {
            get;
            set;
        }
        public double size
        {
            get;
            set;
        }
        public double remsize
        {
            get
            {
                lock (lockObject)
                {
                    if (Fullyfilled == true)
                        return 0;

                    if (Fills == null || Fills.Count == 0)
                        return size;

                    double filledQty = 0;
                    while (true)
                    {
                        try
                        {
                            foreach (var f in Fills)
                            {
                                filledQty += f.Amount;
                            }
                            return size - filledQty;
                        }
                        catch (InvalidOperationException ex)
                        {
                        }
                    }
                }
            }
        }

        public string GetShortOrderId() { return this.Guid.ToString().Substring(0, 8); }

        public bool Fullyfilled
        {
            get;
            private set;
        }

        public Guid Guid
        {
            get;
            private set;
        }

        public bool IsCancelled
        {
            get; set;
        }

        public OrderFilledDlg FillCallback;

        public override bool Equals(object obj)
        {
            Order other = obj as Order;
            if(other != null)
            {
                return other.Guid == this.Guid; 
                //return (this.size == other.size) && (this.price == other.price) && (this.type == other.type);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            // return (this.size.GetHashCode() + this.price.GetHashCode() + this.type.GetHashCode());
            return this.Guid.GetHashCode();
        }

        public List<Fill> Fills
        {
            get;
            set;
        }

        public override string ToString()
        {
            return $"Order: {type},{price},{size},{remsize},{Guid}";
        }
    }
}

