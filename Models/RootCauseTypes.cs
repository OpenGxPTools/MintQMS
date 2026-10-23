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

namespace OpenQMS.Models
{
    public enum DeviationEventType
    {
        [Display(Name = "Process Deviation")]
        ProcessDeviation,

        [Display(Name = "Supplier Non-conformance")]
        SupplierNonConformance,

        [Display(Name = "Quality Incident")]
        QualityIncident,

        [Display(Name = "Equipment Failure")]
        EquipmentFailure,

        [Display(Name = "Out of Specification (OOS)")]
        OutOfSpecification,

        [Display(Name = "Adverse Trend")]
        AdverseTrend,

        [Display(Name = "Near Miss")]
        NearMiss
    }

    public enum DeviationSeverity
    {
        [Display(Name = "Critical")]
        Critical,

        [Display(Name = "Major")]
        Major,

        [Display(Name = "Minor")]
        Minor
    }

    public enum RootCauseCategory
    {
        [Display(Name = "Equipment/Instrumentation")]
        Equipment,
        
        [Display(Name = "Materials/Raw Materials")]
        Materials,
        
        [Display(Name = "Personnel/Training")]
        Personnel,
        
        [Display(Name = "Procedure/Documentation")]
        Procedure,
        
        [Display(Name = "Process/Manufacturing")]
        Process,
        
        [Display(Name = "Environment/Facility")]
        Environment,
        
        [Display(Name = "Quality System")]
        QualitySystem,
        
        [Display(Name = "Computer System/Data Integrity")]
        ComputerSystem,
        
        [Display(Name = "Utilities (HVAC, Water, etc.)")]
        Utilities,
        
        [Display(Name = "Testing/Laboratory")]
        Testing,
        
        [Display(Name = "Vendor/Supplier")]
        Vendor,
        
        [Display(Name = "Other - Specify in investigation notes")]
        Other
    }
}
