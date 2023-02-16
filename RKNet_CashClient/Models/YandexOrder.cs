using System.Collections.Generic;

namespace RKNet_CashClient.Models
{
    public class YandexOrder
    {
        public string discriminator { get; set; }
        public string eatsId { get; set; }
        public string restaurantId { get; set; }
        public DeliveryInfo deliveryInfo { get; set; }
        public PaymentInfo paymentInfo { get; set; }
        public List<OrderItem> items { get; set; } = new List<OrderItem>();
        public int? persons { get; set; }
        public string comment { get; set; }
        public List<OrderPromo> promos { get; set; } = new List<OrderPromo>();

        // Подклассы ////////////////////////////////////////////////////////////////////
        public class DeliveryInfo
        {
            public string clientName { get; set; }
            public string phoneNumber { get; set; }
            public string courierArrivementDate { get; set; }
        }

        public class PaymentInfo
        {            
            public double itemsCost { get; set; }
            public string paymentType { get; set; }            
        }

        public class OrderItem
        {
            public string id { get; set; }
            public string name { get; set; }
            public float quantity { get; set; }
            public double price { get; set; }
            public List<OrderItemModifications> modifications { get; set; } = new List<OrderItemModifications>();
            public List<string> promos { get; set; } = new List<string>();
        }

        public class OrderItemModifications
        {
            public string id { get; set; }
            public string name { get; set; }
            public int quantity { get; set; }
            public double price { get; set; }
        }

        public class OrderPromo
        {
            public string type { get; set; }
            public double discount { get; set; }
        }
        public class Response
        {
            public string result { get; set; }
            public string orderId { get; set; }
        }

        public class ResponseUpdate
        {
            public string result = "OK";
        }
    }    
}
