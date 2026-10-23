using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class ComplaintProduct
    {
        public int ComplaintId { get; set; }
        [ForeignKey("ComplaintId")]
        public virtual Complaint? Complaint { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }

    public class ComplaintProcess
    {
        public int ComplaintId { get; set; }
        [ForeignKey("ComplaintId")]
        public virtual Complaint? Complaint { get; set; }
        public int ProcessId { get; set; }
        [ForeignKey("ProcessId")]
        public virtual Process? Process { get; set; }
    }

    public class ComplaintMaterial
    {
        public int ComplaintId { get; set; }
        [ForeignKey("ComplaintId")]
        public virtual Complaint? Complaint { get; set; }
        public int MaterialId { get; set; }
        [ForeignKey("MaterialId")]
        public virtual Material? Material { get; set; }
    }

    public class ComplaintSupplier
    {
        public int ComplaintId { get; set; }
        [ForeignKey("ComplaintId")]
        public virtual Complaint? Complaint { get; set; }
        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
    }
}
