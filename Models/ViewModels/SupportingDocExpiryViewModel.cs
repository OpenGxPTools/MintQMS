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

namespace OpenQMS.Models.ViewModels
{
    public class SupportingDocItem
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int ReminderDays { get; set; }
        public bool IsExpired { get; set; }
        public bool IsExpiringSoon { get; set; }
    }

    public class SupportingDocParentGroup
    {
        public int ParentId { get; set; }
        public string ParentIdentifier { get; set; } = string.Empty;
        public string ParentName { get; set; } = string.Empty;
        public List<SupportingDocItem> Documents { get; set; } = new();
    }

    public class SupportingDocModuleGroup
    {
        public string ModuleName { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public List<SupportingDocParentGroup> Parents { get; set; } = new();
        public int ExpiredCount => Parents.Sum(p => p.Documents.Count(d => d.IsExpired));
        public int ExpiringSoonCount => Parents.Sum(p => p.Documents.Count(d => d.IsExpiringSoon));
    }

    public class SupportingDocExpiryViewModel
    {
        public List<SupportingDocModuleGroup> Modules { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public int TotalExpired => Modules.Sum(m => m.ExpiredCount);
        public int TotalExpiringSoon => Modules.Sum(m => m.ExpiringSoonCount);
    }
}
