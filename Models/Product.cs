/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2025  C-realize IT Services SRL (https://www.c-realize.com)

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://www.c-realize.com/contact.  For AGPL licensing, see below.

AGPL:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see http://www.gnu.org/licenses/.
*/

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenQMS.Models
{
    public class Product
    {
        public Product()
        {
            this.Changes = new HashSet<Change>();
        }

        public int Id { get; set; }
        [Display(Name = "ID")]
        public string ProductId { get; set; }
        public string? GeneratedFrom { get; set; }
        public string Name { get; set; }

        [Display(Name = "Product Type")]
        public ProductType? ProductType { get; set; }

        // Pharmaceutical / Biological / Consumer Health
        [Display(Name = "INN / Generic Name")]
        public string? InnName { get; set; }
        [Display(Name = "Brand / Trade Name")]
        public string? BrandName { get; set; }
        [Display(Name = "Dosage Form")]
        public string? DosageForm { get; set; }
        [Display(Name = "ARTG Entry / Regulatory Reference")]
        public string? ArtgReference { get; set; }

        // Cosmetics
        [Display(Name = "INCI Name / Ingredient Declaration Reference")]
        public string? InciName { get; set; }

        // Cosmetics + Raw Materials (shared field, different UI options by type)
        [Display(Name = "Presentation / Form")]
        public string? PresentationForm { get; set; }

        // Medical Device
        [Display(Name = "Device Type / Category")]
        public string? DeviceTypeCategory { get; set; }
        [Display(Name = "Model / Version")]
        public string? ModelVersion { get; set; }

        // Raw Material
        [Display(Name = "Standard / Scientific Name")]
        public string? StandardScientificName { get; set; }
        [Display(Name = "Material Type / Category")]
        public string? RawMaterialTypeCategory { get; set; }
        [Display(Name = "Grade (if applicable)")]
        public string? Grade { get; set; }
        public string Description { get; set; }
        public decimal Version { get; set; }
        [Display(Name = "Edited By")]
        public string? EditedBy { get; set; }
        [Display(Name = "Edited On")]
        public DateTime EditedOn { get; set; }
        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }
        [Display(Name = "Approved On")]
        public DateTime? ApprovedOn { get; set; }
        public bool IsLocked { get; set; }
        public bool IsDeleted { get; set; } = false;
        public string? DeletedBy { get; set; }
        public DateTime? DeletedOn { get; set; }
        public string? ExportFilePath { get; set; }
        public virtual ICollection<Process>? Processes { get; set; }
        public virtual ICollection<Change>? Changes { get; set; }
        public virtual ICollection<Deviation>? Deviations { get; set; }
        public virtual ICollection<Capa>? Capas { get; set; }
        public virtual ICollection<ComplaintProduct>? LinkedComplaints { get; set; }
        public virtual ICollection<ProductDocument>? Documents { get; set; }
        public virtual ICollection<ProductLinkedDocument>? RelatedDocuments { get; set; }
        public virtual ICollection<ProductStrength>? Strengths { get; set; }
        public virtual ICollection<ProductSpecLine>? Specifications { get; set; }
        public ProductStatus Status { get; set; }
        public enum ProductStatus
        {
            Draft,
            Approved,
            Obsolete,
            [Display(Name = "Under Review")]
            UnderReview
        }
    }
}
