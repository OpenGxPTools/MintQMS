using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class ProductSpecLine
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int DisplayOrder { get; set; }

        [Required, MaxLength(200)]
        [Display(Name = "Test Parameter")]
        public string TestParameter { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Method")]
        public string? Method { get; set; }

        [Required, MaxLength(300)]
        [Display(Name = "Acceptance Criterion")]
        public string AcceptanceCriterion { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Comments")]
        public string? Comments { get; set; }

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public virtual Product? Product { get; set; }
    }
}
