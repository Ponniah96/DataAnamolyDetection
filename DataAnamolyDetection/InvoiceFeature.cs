using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAnamolyDetection
{
    public class InvoiceFeature
    {
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public int LineCount { get; set; }
        public decimal AvgLineAmount { get; set; }
        public double CustomerAvg { get; set; }
        public double ProjectAvg { get; set; }

        public double[] ToMlVector()
        {
            return new[]
            {
            (double)TotalAmount,
            (double)TaxAmount,
            (double)DiscountAmount,
            (double)LineCount,
            (double)AvgLineAmount,
        };
        }
    }
}
