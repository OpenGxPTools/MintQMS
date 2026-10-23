using System.ComponentModel.DataAnnotations;

namespace OpenQMS.Models
{
    public class PVLiteratureSearch
    {
        public int Id { get; set; }

        [Display(Name = "Search Date")]
        public DateTime SearchDate { get; set; }

        [Display(Name = "Searched By")]
        public string SearchedBy { get; set; } = string.Empty;

        [Display(Name = "Databases Searched")]
        public string? DatabasesSearched { get; set; }

        [Display(Name = "Search Terms")]
        public string? SearchTerms { get; set; }

        [Display(Name = "Date Range From")]
        public DateTime? DateRangeFrom { get; set; }

        [Display(Name = "Date Range To")]
        public DateTime? DateRangeTo { get; set; }

        [Display(Name = "Total Results")]
        public int ResultsCount { get; set; }

        [Display(Name = "Relevant Results")]
        public int RelevantResults { get; set; }

        [Display(Name = "AEs Identified")]
        public int AEsIdentified { get; set; }

        public string? Notes { get; set; }

        public virtual ICollection<PVLiteratureAuditLog>? AuditLogs { get; set; }
    }
}
