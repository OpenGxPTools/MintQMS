using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class MaintenanceDeviation
    {
        public int MaintenanceId { get; set; }
        [ForeignKey("MaintenanceId")]
        public virtual Maintenance? Maintenance { get; set; }
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
    }

    public class MaintenanceCapa
    {
        public int MaintenanceId { get; set; }
        [ForeignKey("MaintenanceId")]
        public virtual Maintenance? Maintenance { get; set; }
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
    }
}
