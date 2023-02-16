using System.Collections.Generic;

namespace RKNet_CashClient.Models
{
    public class DeliveryOrder
    {
        public string originalOrderId { get; set; }
        public bool preOrder { get; set; }
        public string createdAt { get; set; }
        public Customer customer { get; set; } = new Customer();
        public Payment payment { get; set; }  = new Payment();
        public string expeditionType { get; set; }
        public Delivery delivery { get; set; } = new Delivery();
        public Pickup pickup { get; set; }  = new Pickup();
        public List<OrderProduct> products { get; set; } = new List<OrderProduct>();
        public List<OrderPromotion> promotions { get; set; } = new List<OrderPromotion>();
        public string comment { get; set; }
        public Price price { get; set; } = new Price();
        public int personsQuantity { get; set; }
        public CallCenter callCenter { get; set; } = new CallCenter();
        public Courier courier { get; set; }
        public PartnerDiscountInfo partnerDiscountInfo { get; set; }


        public class Customer
        {
            public string name { get; set; }
            public string phone { get; set; }
            public string email { get; set; }
        }
        public class Payment
        {
            public string type { get; set; }
            public string requiredMoneyChange { get; set; }
        }
        public class Delivery
        {
            public string expectedTime { get; set; }
            public Address address { get; set; } = new Address();
            public class Address
            {
                public string subway { get; set; }
                public string region { get; set; }
                public City city { get; set; } = new City();
                public Street street { get; set; } = new Street();
                public string houseNumber { get; set; }
                public string flatNumber { get; set; }
                public string entrance { get; set; }
                public string intercom { get; set; }
                public string floor { get; set; }
                public Coordinates coordinates { get; set; } = new Coordinates();

                public class City
                {
                    public string name { get; set; }
                    public string code { get; set; }
                }
                public class Street
                {
                    public string name { get; set; }
                    public string code { get; set; }
                }
                public class Coordinates
                {
                    public string latitude { get; set; }
                    public string longitude { get; set; }
                }
            }
        }
        public class Pickup
        {
            public string expectedTime { get; set; }
            public string taker { get; set; }
        }
        public class OrderProduct
        {
            public string id { get; set; }
            public string name { get; set; }
            public string price { get; set; }
            public string quantity { get; set; }
            public string promotionId { get; set; }
            public List<OrderIngredient> ingredients { get; set; } = new List<OrderIngredient>();

            public class OrderIngredient
            {
                public string id { get; set; }
                public string name { get; set; }
                public int price { get; set; }
                public string groupName { get; set; }
            }
        }
        public class OrderPromotion
        {
            public string id { get; set; }
            public string name { get; set; }
        }
        public class Price
        {
            public int total { get; set; }
            public int deliveryFee { get; set; }
            public int discount { get; set; }
        }
        public class CallCenter
        {
            public string phone { get; set; }
        }
        public class Courier
        {
            public string name { get; set; }
            public string phone { get; set; }
        }
        public class PartnerDiscountInfo
        {
            public string totalDiscount { get; set; }
            public string partnerPayment { get; set; }
            public string dcPayment { get; set; }
        }
    }
}
