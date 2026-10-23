using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class AssetLinkedDocument
    {
        public int AssetId { get; set; }
        [ForeignKey("AssetId")]
        public virtual Asset? Asset { get; set; }

        public int AppDocumentId { get; set; }
        [ForeignKey("AppDocumentId")]
        public virtual AppDocument? Document { get; set; }

        public decimal LinkedVersion { get; set; }
    }

    public class CustomerLinkedDocument
    {
        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        public int AppDocumentId { get; set; }
        [ForeignKey("AppDocumentId")]
        public virtual AppDocument? Document { get; set; }

        public decimal LinkedVersion { get; set; }
    }

    public class MaterialLinkedDocument
    {
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }

        public int AppDocumentId { get; set; }
        [ForeignKey("AppDocumentId")]
        public virtual AppDocument? Document { get; set; }

        public decimal LinkedVersion { get; set; }
    }

    public class ProcessLinkedDocument
    {
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }

        public int AppDocumentId { get; set; }
        [ForeignKey("AppDocumentId")]
        public virtual AppDocument? Document { get; set; }

        public decimal LinkedVersion { get; set; }
    }

    public class ProductLinkedDocument
    {
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        public int AppDocumentId { get; set; }
        [ForeignKey("AppDocumentId")]
        public virtual AppDocument? Document { get; set; }

        public decimal LinkedVersion { get; set; }
    }

    public class ServiceLinkedDocument
    {
        public int ServiceId { get; set; }
        [ForeignKey("ServiceId")]
        public virtual Service? Service { get; set; }

        public int AppDocumentId { get; set; }
        [ForeignKey("AppDocumentId")]
        public virtual AppDocument? Document { get; set; }

        public decimal LinkedVersion { get; set; }
    }
}
