using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITP244_Ef
{
    [MetadataType(typeof(SalesOrder.SalesOrderMetadata))]
    public partial class SalesOrder
    {
        public decimal OrderTotal => SalesOrderParts.Sum(sop => sop.Quantity * sop.UnitPrice);
        private sealed class SalesOrderMetadata
        {
            [Display(Name = "Order Date", Order = 100)]
            [DisplayFormat(DataFormatString = "{0:d}")]
            public string OrderDate { get; set; }

            [Display(Name = "Order $$$", Order = 100)]
            [DisplayFormat(DataFormatString = "{0:c}")]
            public decimal OrderTotal { get; set; }
        }
    }
}
