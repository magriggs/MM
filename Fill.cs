using System;

namespace MM
{
    public class Fill 
    {
        public Fill(double price,double amount, Order originalOrder)
        {
            this.Price = price;
            this.Amount = amount;
            OriginalOrder = originalOrder;
            this.Guid = Guid.NewGuid();
        }
        public double Price
        {
            get;
            set;
        }
        public double Amount
        {
            get;
            set;
        }
        public Order OriginalOrder { get; }
        public Guid Guid { get; private set; }

        public override string ToString()
        {
            return $"Fill,Price:{Price},Amount:{Amount},FillGuid,{this.Guid},OrderGuid:{OriginalOrder.GetShortOrderId()}";
        }

        public override bool Equals(object obj)
        {
            Fill other = obj as Fill;
            if(other != null)
            {
                return this.Guid == other.Guid;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return (this.Price.GetHashCode() + this.Amount.GetHashCode());
        }
    }
}

