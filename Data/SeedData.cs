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

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Models;

namespace OpenQMS.Data
{
    public class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider, string adminPw = "")
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
            {
                var roleManager = serviceProvider.GetService<RoleManager<AppRole>>();

                // Canonical role definitions. Descriptions mirror the reviewed
                // Role & Permissions Reference (MintQMS_RolePermissions_v2). On every
                // startup each missing role is created, and each existing role whose
                // description has drifted is reconciled to the canonical text below —
                // so the Roles page always reflects the reference document.
                var roleDefinitions = new (string Name, string Description)[]
                {
                    (Constants.AdministratorsRole,
                        "Full, unrestricted access to every module, action, user, role and setting. Only role that can delete records, manage user roles, save system settings, and force-reopen closed records."),
                    (Constants.QualityManagerRole,
                        "Broad access across all quality modules. Can create, edit, manage and approve/reject/close records in Quality Events, Quality Assurance, Quality Operations and Release. Can view users and settings. Cannot delete records (Administrator only)."),
                    (Constants.QualityUserRole,
                        "Can create, edit and advance workflow in Quality Events (Deviations, Complaints, CAPAs, Changes), Suppliers and Release. Cannot approve, accept or close records. Has access to Documents Dashboard and ReviewRegister."),
                    (Constants.RegulatoryManagerRole,
                        "Full management of Regulatory Intelligence, Licences & Certifications, and all PV sub-modules. Can approve and close regulatory records. Can create and manage Training records. Can view Settings, Users and Roles. No access to Quality Event, Maintenance or Release workflows."),
                    (Constants.RegulatoryUserRole,
                        "Can create and edit records in Regulatory Intelligence, Licences & Certifications, and Pharmacovigilance. Can submit PV cases for review but cannot approve. No access to quality event workflows, Maintenances, Release or most quality-only modules."),
                    (Constants.AuthorisedPersonRole,
                        "Legally defined batch release sign-off role (PIC/S Annex 16). Sole authority to certify and reject batch releases and issue release certificates. Participates in Quality Event workflows (create, edit, approve). Cannot author or approve documents. Cannot access Regulatory or PV modules."),
                    (Constants.AuditorRole,
                        "Access to Quality Events, Risks (create/edit/submit), Quality Operations, Release (view), Audits (create/manage up to UnderReview — cannot approve/close), Training Dashboard (view), Settings (view), User/Role/Login Audit (view). Can create and edit CAPAs to support audit findings."),
                    (Constants.TrainerRole,
                        "Can create, assign, edit and complete Training records. Has view access to Maintenances and Release. Cannot access quality event workflows, approval steps or regulatory modules."),
                    (Constants.ReadOnlyRole,
                        "View-only access to registers, dashboards and record details across accessible modules. Cannot create, edit, submit, upload, delete or approve anything. Intended for internal auditors (view-only), authorised non-QA personnel or observers."),
                    (Constants.OperationsUserRole,
                        "Can create Deviations, Complaints, CAPAs, Changes, Suppliers. Can create/edit Suppliers and addresses. Can create and perform Maintenance tasks (StartWork, CompleteWork). Can create and edit draft Release records. Cannot approve or advance quality/release workflows. No access to Regulatory, Risks, Audits, Reports or Settings."),
                    (Constants.CommercialUserRole,
                        "Can create Deviation, Complaint, CAPA, Change and Supplier records. Can view most quality module registers. No access to Release, Maintenances, Regulatory, Risks, Audits, Reports or Settings. Cannot approve or advance any workflow."),
                };

                foreach (var (name, description) in roleDefinitions)
                {
                    var role = await roleManager!.FindByNameAsync(name);
                    if (role == null)
                    {
                        await roleManager.CreateAsync(new AppRole { Name = name, Description = description });
                    }
                    else if (role.Description != description)
                    {
                        // Reconcile drifted description to the canonical text.
                        role.Description = description;
                        await roleManager.UpdateAsync(role);
                    }
                }

                // Ensure the system admin user exists — created only if absent and a password is provided
                var userManager = serviceProvider.GetService<UserManager<AppUser>>();
                if (userManager != null && !string.IsNullOrEmpty(adminPw))
                {
                    const string adminEmail = "admin@trial.local";
                    if (await userManager.FindByEmailAsync(adminEmail) == null)
                    {
                        var adminUser = new AppUser
                        {
                            UserName = adminEmail,
                            Email = adminEmail,
                            EmailConfirmed = true,
                            // FirstName/LastName are NOT NULL — omitting them crashes the
                            // first-boot seed (SQLite NOT NULL constraint on AspNetUsers).
                            FirstName = "System",
                            LastName = "Administrator"
                        };
                        var result = await userManager.CreateAsync(adminUser, adminPw);
                        if (result.Succeeded)
                            await userManager.AddToRoleAsync(adminUser, Constants.AdministratorsRole);
                    }
                }

                SeedDB(context, "");
            }
        }

        public static void SeedDB(ApplicationDbContext context, string adminID)
        {
            // Seed RegIntel reference data unconditionally (has its own guard)
            RISeedData.Seed(context);

            // Seed LicCert reference data unconditionally (has its own guard)
            LCSeedData.Seed(context);

            if (context.AppDocument.Any())
            {
                return;   // DB has been seeded
            }

            context.SaveChanges();
        }
    }
}
