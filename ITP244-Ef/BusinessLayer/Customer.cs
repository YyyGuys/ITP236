using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITP244_Ef
{
    [MetadataType(typeof(CustomerMetadata))]
    public partial class Customer
    {
        private sealed class CustomerMetadata
        {
            [Display(Name = "First Name", Order = 100)]
            public string FirstName { get; set; }

            [Display(Name = "Surname", Order = 100)]
            public string LastName { get; set; }
        }
    }
}
