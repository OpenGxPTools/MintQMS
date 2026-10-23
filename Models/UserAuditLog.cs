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

namespace OpenQMS.Models
{
    public class UserAuditLog
    {
        public int Id { get; set; }

        // The user this log entry is about
        public int UserId { get; set; }
        public AppUser User { get; set; }

        // What happened: "User Created", "Role Added", "Role Removed"
        public string Action { get; set; }

        // The role name for role events; null for creation events
        public string? Detail { get; set; }

        // Who performed the action
        public string PerformedBy { get; set; }

        public DateTime PerformedOn { get; set; }
    }
}
