using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class DeviationProduct
    {
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }

    public class DeviationProcess
    {
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
    }

    public class DeviationAsset
    {
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }
    }

    public class DeviationMaterial
    {
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
    }

    public class DeviationSupplier
    {
        public int DeviationId { get; set; }
        [ForeignKey("DeviationId")]
        public virtual Deviation? Deviation { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
    }
}
