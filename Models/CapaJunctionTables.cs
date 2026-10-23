using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class CapaProduct
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }

    public class CapaProcess
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
    }

    public class CapaAsset
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }
    }

    public class CapaMaterial
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
    }

    public class CapaSupplier
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
    }

    public class CapaDeviation
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
    }

    public class CapaComplaint
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int ComplaintId { get; set; }
        [ForeignKey("ComplaintId")]
        public virtual Complaint? Complaint { get; set; }
    }

    public class CapaAudit
    {
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
        public int AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
    }
}
