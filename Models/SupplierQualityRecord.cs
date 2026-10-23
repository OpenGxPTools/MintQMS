using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class SupplierQualityRecord
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        [Display(Name = "Record Type")]
        public QualityRecordType RecordType { get; set; }

        [Display(Name = "Deviation ID")]
        public int? DeviationId { get; set; }

        [Display(Name = "CAPA ID")]
        public int? CapaId { get; set; }

        [Display(Name = "Change ID")]
        public int? ChangeId { get; set; }

        [Display(Name = "Complaint ID")]
        public int? ComplaintId { get; set; }

        [Display(Name = "Risk ID")]
        public int? RiskId { get; set; }

        [Display(Name = "Audit ID")]
        public int? AuditId { get; set; }

        [Display(Name = "Review ID")]
        public int? ReviewId { get; set; }

        public string? Notes { get; set; }

        [Display(Name = "Linked On")]
        public DateTime LinkedOn { get; set; }

        [Display(Name = "Linked By")]
        public string? LinkedBy { get; set; }
    }

    public enum QualityRecordType
    {
        Deviation,
        CAPA,
        Change,
        Complaint,
        Risk,
        Audit
    }
}
