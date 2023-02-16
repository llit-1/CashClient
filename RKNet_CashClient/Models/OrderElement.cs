using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RKNet_CashClient.Models
{
    internal class OrderElement
    {
        public string Created { get; set; }
        public string Number { get; set; }
        public string AgregatorNumber { get; set; }
        public string Customer { get; set; }
        public string Operator { get; set; }
        public string Status { get; set; }
        public string TotalSum { get; set; }
        public bool isActive { get; set; }
    }
}
