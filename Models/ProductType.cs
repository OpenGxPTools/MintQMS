using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public enum ProductType
    {
        Pharmaceutical,
        Biological,
        [Display(Name = "Medical Device")]
        MedicalDevice,
        [Display(Name = "Consumer Health")]
        ConsumerHealth,
        Cosmetic,
        [Display(Name = "Raw Material")]
        RawMaterial
    }
}
