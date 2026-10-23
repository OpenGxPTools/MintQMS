using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class RiskProduct
    {
        public int RiskId { get; set; }
        [ForeignKey("RiskId")]
        public virtual Risk? Risk { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }

    public class RiskProcess
    {
        public int RiskId { get; set; }
        [ForeignKey("RiskId")]
        public virtual Risk? Risk { get; set; }
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
    }

    public class RiskAsset
    {
        public int RiskId { get; set; }
        [ForeignKey("RiskId")]
        public virtual Risk? Risk { get; set; }
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }
    }

    public class RiskMaterial
    {
        public int RiskId { get; set; }
        [ForeignKey("RiskId")]
        public virtual Risk? Risk { get; set; }
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
    }

    public class RiskSupplier
    {
        public int RiskId { get; set; }
        [ForeignKey("RiskId")]
        public virtual Risk? Risk { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
    }
}
