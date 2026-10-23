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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Auditor")]
    public class LoginAuditController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoginAuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? userName, string? actionFilter, DateTime? from, DateTime? to)
        {
            var query = _context.LoginAuditLog.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userName))
                query = query.Where(l => l.UserName.Contains(userName));

            if (!string.IsNullOrWhiteSpace(actionFilter))
                query = query.Where(l => l.Action == actionFilter);

            if (from.HasValue)
                query = query.Where(l => l.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(l => l.Timestamp <= to.Value.AddDays(1));

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();

            ViewBag.UserName     = userName;
            ViewBag.ActionFilter = actionFilter;
            ViewBag.From         = from?.ToString("yyyy-MM-dd");
            ViewBag.To           = to?.ToString("yyyy-MM-dd");

            return View(logs);
        }
    }
}
