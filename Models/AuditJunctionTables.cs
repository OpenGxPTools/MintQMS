using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AuditProduct
    {
        public int AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }

    public class AuditProcess
    {
        public int AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
    }

    public class AuditAsset
    {
        public int AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }
    }

    public class AuditMaterial
    {
        public int AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
    }

    public class AuditSupplier
    {
        public int AuditId { get; set; }
        [ForeignKey("AuditId")]
        public virtual Audit? Audit { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
    }
}
