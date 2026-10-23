using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class ChangeProduct
    {
        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }

    public class ChangeProcess
    {
        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
    }

    public class ChangeAsset
    {
        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }
    }

    public class ChangeMaterial
    {
        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
    }

    public class ChangeSupplier
    {
        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
    }

    public class ChangeCapa
    {
        public int ChangeId { get; set; }
        [ForeignKey("ChangeId")]
        public virtual Change? Change { get; set; }
        public int CapaId { get; set; }
        [ForeignKey("CapaId")]
        public virtual Capa? Capa { get; set; }
    }
}
