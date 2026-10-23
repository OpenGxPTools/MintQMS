using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AEPatient
    {
        public int Id { get; set; }

        public int AdverseEventId { get; set; }

        [ForeignKey("AdverseEventId")]
        public virtual AdverseEvent? AdverseEvent { get; set; }

        [Display(Name = "Patient Initials / ID")]
        public string? PatientIdentifier { get; set; }

        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        public int? Age { get; set; }

        [Display(Name = "Age Unit")]
        public PatientAgeUnit? AgeUnit { get; set; }

        public PatientSex? Sex { get; set; }

        [Display(Name = "Weight (kg)")]
        [Column(TypeName = "decimal(5,1)")]
        public decimal? WeightKg { get; set; }

        [Display(Name = "Height (cm)")]
        [Column(TypeName = "decimal(5,1)")]
        public decimal? HeightCm { get; set; }

        [Display(Name = "Medical History")]
        public string? MedicalHistory { get; set; }

        [Display(Name = "Concomitant Medications")]
        public string? ConcomitantMedications { get; set; }

        [Display(Name = "Pregnant")]
        public bool? IsPregnant { get; set; }

        [Display(Name = "Last Menstrual Period")]
        public DateTime? LastMenstrualPeriod { get; set; }

        public enum PatientSex
        {
            Male,
            Female,
            Unknown,
            [Display(Name = "Not Applicable")]
            NotApplicable
        }

        public enum PatientAgeUnit
        {
            Years,
            Months,
            Weeks,
            Days,
            Hours
        }
    }
}
