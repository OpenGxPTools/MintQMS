using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class ProductStrength
    {
        public int Id { get; set; }
        public int ProductId { get; set; }

        [Required]
        [Display(Name = "Strength")]
        public string Value { get; set; } = string.Empty;

        public virtual Product? Product { get; set; }
    }
}
