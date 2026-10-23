using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class SupplierAddress
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        [Display(Name = "Post Code")]
        public string? PostCode { get; set; }

        public string? Country { get; set; }
        public string? Notes { get; set; }
    }
}
