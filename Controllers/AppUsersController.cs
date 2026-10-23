/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.ViewModels;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Auditor")]
    public class AppUsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly ApplicationDbContext _context;
        public AppUsersController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context     = context;
        }

        // GET: UsersController
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRolesViewModel = new List<UserRolesViewModel>();
            foreach (AppUser user in users)
            {
                var thisViewModel = new UserRolesViewModel();
                thisViewModel.UserId   = user.Id.ToString();
                thisViewModel.UserName = user.UserName;
                thisViewModel.Email    = user.Email;
                thisViewModel.IsActive = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow;
                thisViewModel.Roles    = await _userManager.GetRolesAsync(user);
                userRolesViewModel.Add(thisViewModel);
            }
            return View(userRolesViewModel);
        }

        // GET: UsersController/Create — users are created on the Identity Register page
        // (Administrator only); this scaffolded route never had a view of its own.
        [Authorize(Roles = "Administrator")]
        public ActionResult Create()
        {
            return RedirectToPage("/Account/Register", new { area = "Identity" });
        }

        // GET: UsersController/History/userId
        public async Task<IActionResult> History(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var logs = await _context.UserAuditLog
                .Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            ViewBag.UserName = user.UserName;
            ViewBag.Email    = user.Email;
            return View(logs);
        }

        // GET: UsersController/ManageRoles/userId
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ManageRoles(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles  = await _roleManager.Roles.ToListAsync();

            var vm = new ManageUserRolesViewModel
            {
                UserId          = userId,
                UserName        = user.UserName,
                Email           = user.Email,
                IsActive        = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow,
                APQualification = user.APQualification,
                Roles           = allRoles.Select(r => new RoleSelection
                {
                    RoleName    = r.Name,
                    Description = r.Description,
                    IsSelected  = userRoles.Contains(r.Name)
                }).ToList()
            };

            return View(vm);
        }

        // POST: UsersController/ManageRoles
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> ManageRoles(ManageUserRolesViewModel vm)
        {
            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            var currentRoles  = await _userManager.GetRolesAsync(user);
            var selectedRoles = vm.Roles.Where(r => r.IsSelected).Select(r => r.RoleName).ToList();

            var oldApQual = user.APQualification;
            var newApQual = string.IsNullOrWhiteSpace(vm.APQualification) ? null : vm.APQualification.Trim();
            user.APQualification = newApQual;
            await _userManager.UpdateAsync(user);

            var toAdd    = selectedRoles.Except(currentRoles).ToList();
            var toRemove = currentRoles.Except(selectedRoles).ToList();

            if (toAdd.Any())    await _userManager.AddToRolesAsync(user, toAdd);
            if (toRemove.Any()) await _userManager.RemoveFromRolesAsync(user, toRemove);

            var performedBy = User.Identity?.Name ?? "System";
            var now         = DateTime.UtcNow;

            // Field-level change tracking: AP Qualification
            if ((oldApQual ?? "") != (newApQual ?? ""))
                _context.UserAuditLog.Add(new UserAuditLog
                {
                    UserId      = user.Id,
                    Action      = "AP Qualification Changed",
                    Detail      = $"\"{oldApQual ?? "(none)"}\" → \"{newApQual ?? "(none)"}\"",
                    PerformedBy = performedBy,
                    PerformedOn = now
                });

            // Handle active/inactive toggle — prevent self-deactivation
            bool currentlyActive = !user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow;
            var currentUserId = _userManager.GetUserId(User);
            if (vm.UserId == currentUserId && !vm.IsActive)
            {
                TempData["Error"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            if (vm.IsActive && !currentlyActive)
            {
                var result = await _userManager.SetLockoutEndDateAsync(user, null);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Failed to activate account: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Index));
                }
                _context.UserAuditLog.Add(new UserAuditLog
                {
                    UserId      = user.Id,
                    Action      = "Account Activated",
                    Detail      = $"Activated by {performedBy}",
                    PerformedBy = performedBy,
                    PerformedOn = now
                });
            }
            else if (!vm.IsActive && currentlyActive)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
                if (!result.Succeeded)
                {
                    TempData["Error"] = "Failed to deactivate account: " + string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Index));
                }
                _context.UserAuditLog.Add(new UserAuditLog
                {
                    UserId      = user.Id,
                    Action      = "Account Deactivated",
                    Detail      = $"Deactivated by {performedBy}",
                    PerformedBy = performedBy,
                    PerformedOn = now
                });
            }

            foreach (var role in toAdd)
                _context.UserAuditLog.Add(new UserAuditLog
                {
                    UserId      = user.Id,
                    Action      = "Role Added",
                    Detail      = role,
                    PerformedBy = performedBy,
                    PerformedOn = now
                });

            foreach (var role in toRemove)
                _context.UserAuditLog.Add(new UserAuditLog
                {
                    UserId      = user.Id,
                    Action      = "Role Removed",
                    Detail      = role,
                    PerformedBy = performedBy,
                    PerformedOn = now
                });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"User {user.UserName} updated.";
            return RedirectToAction(nameof(Index));
        }
    }
}
