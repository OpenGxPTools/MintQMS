using OpenQMS.Models.RI;

namespace OpenQMS.Data
{
    public static class RISeedData
    {
        public static void Seed(ApplicationDbContext context)
        {
            // Only skip if both jurisdictions AND types are already seeded
            if (context.RIJurisdictions.Any() && context.RIApplicationTypes.Any()) return;

            if (!context.RIJurisdictions.Any())
            {
            var jurisdictions = new List<RIJurisdiction>
            {
                new() { Code="AU", Name="Australia",      Authority="TGA",           AuthorityUrl="https://www.tga.gov.au",                        PortalUrl="https://portal.tga.gov.au",                CurrencyCode="AUD", SortOrder=1  },
                new() { Code="NZ", Name="New Zealand",    Authority="Medsafe",       AuthorityUrl="https://www.medsafe.govt.nz",                   PortalUrl="https://www.medsafe.govt.nz/prism",        CurrencyCode="NZD", SortOrder=2  },
                new() { Code="EU", Name="European Union", Authority="EMA",           AuthorityUrl="https://www.ema.europa.eu",                     PortalUrl="https://esubmission.ema.europa.eu",        CurrencyCode="EUR", SortOrder=3  },
                new() { Code="US", Name="United States",  Authority="FDA",           AuthorityUrl="https://www.fda.gov",                           PortalUrl="https://gateway.fda.gov",                  CurrencyCode="USD", SortOrder=4  },
                new() { Code="CA", Name="Canada",         Authority="Health Canada", AuthorityUrl="https://www.canada.ca/en/health-canada.html",   PortalUrl="https://hpr-rps.hres.ca",                 CurrencyCode="CAD", SortOrder=5  },
                new() { Code="SG", Name="Singapore",      Authority="HSA",           AuthorityUrl="https://www.hsa.gov.sg",                        PortalUrl="https://www.hsa.gov.sg/prism",             CurrencyCode="SGD", SortOrder=6  },
                new() { Code="MY", Name="Malaysia",       Authority="NPRA",          AuthorityUrl="https://npra.gov.my",                           PortalUrl="https://quest3.npra.gov.my",               CurrencyCode="MYR", SortOrder=7  },
                new() { Code="BR", Name="Brazil",         Authority="ANVISA",        AuthorityUrl="https://www.gov.br/anvisa",                     PortalUrl="https://www.gov.br/anvisa/peticionamento",  CurrencyCode="BRL", SortOrder=8  },
                new() { Code="MX", Name="Mexico",         Authority="COFEPRIS",      AuthorityUrl="https://www.gob.mx/cofepris",                   PortalUrl="https://tramites.cofepris.gob.mx",         CurrencyCode="MXN", SortOrder=9  },
                new() { Code="CO", Name="Colombia",       Authority="INVIMA",        AuthorityUrl="https://www.invima.gov.co",                     PortalUrl="https://tramites.invima.gov.co",           CurrencyCode="COP", SortOrder=10 },
            };
            context.RIJurisdictions.AddRange(jurisdictions);
            context.SaveChanges();
            } // end if !RIJurisdictions.Any()

            if (!context.RIApplicationTypes.Any())
            {
            var j = context.RIJurisdictions.ToDictionary(x => x.Code);

            var types = new List<RIApplicationType>
            {
                // ═══ AUSTRALIA (TGA) ═══════════════════════════════════════════════
                new() { JurisdictionId=j["AU"].Id, Code="Cat1-NCE",    Name="Category 1 — New Chemical Entity",                   Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=255,  SortOrder=1  },
                new() { JurisdictionId=j["AU"].Id, Code="Cat1-NewInd", Name="Category 1 — New Indication",                        Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=255,  SortOrder=2  },
                new() { JurisdictionId=j["AU"].Id, Code="Cat2-COR",    Name="Category 2 / COR — Comparable Overseas Regulator",   Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=120,  SortOrder=3  },
                new() { JurisdictionId=j["AU"].Id, Code="PR",          Name="Priority Review",                                    Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=150,  SortOrder=4  },
                new() { JurisdictionId=j["AU"].Id, Code="PA",          Name="Provisional Approval",                               Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=5  },
                new() { JurisdictionId=j["AU"].Id, Code="ODD",         Name="Orphan Drug Designation",                            Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=null, SortOrder=6  },
                new() { JurisdictionId=j["AU"].Id, Code="Generic-AU",  Name="Generic Medicine Registration (AUST R)",             Category=RIApplicationCategory.Generic,          RequiresApproval=true,  ClockDays=110,  SortOrder=7  },
                new() { JurisdictionId=j["AU"].Id, Code="Biosim-AU",   Name="Biosimilar Registration",                            Category=RIApplicationCategory.Biosimilar,       RequiresApproval=true,  ClockDays=175,  SortOrder=8  },
                new() { JurisdictionId=j["AU"].Id, Code="OTC-AU",      Name="OTC Medicine Registration",                          Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=9  },
                new() { JurisdictionId=j["AU"].Id, Code="Listed",      Name="Listed Complementary Medicine (AUST L)",             Category=RIApplicationCategory.Notification,     RequiresApproval=false, ClockDays=null, SortOrder=10 },
                new() { JurisdictionId=j["AU"].Id, Code="SAS-A",       Name="Special Access Scheme — Category A",                 Category=RIApplicationCategory.AccessPathway,    RequiresApproval=false, ClockDays=null, SortOrder=11 },
                new() { JurisdictionId=j["AU"].Id, Code="SAS-B",       Name="Special Access Scheme — Category B",                 Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=12 },
                new() { JurisdictionId=j["AU"].Id, Code="SAS-C",       Name="Special Access Scheme — Category C",                 Category=RIApplicationCategory.AccessPathway,    RequiresApproval=false, ClockDays=null, SortOrder=13 },
                new() { JurisdictionId=j["AU"].Id, Code="AP",          Name="Authorised Prescriber Scheme",                       Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=14 },
                new() { JurisdictionId=j["AU"].Id, Code="CTN",         Name="Clinical Trial Notification (CTN)",                  Category=RIApplicationCategory.ClinicalTrial,    RequiresApproval=false, ClockDays=null, SortOrder=15 },
                new() { JurisdictionId=j["AU"].Id, Code="CTA",         Name="Clinical Trial Approval (CTA)",                      Category=RIApplicationCategory.ClinicalTrial,    RequiresApproval=true,  ClockDays=null, SortOrder=16 },
                new() { JurisdictionId=j["AU"].Id, Code="Var-Major",   Name="Major Variation",                                    Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=null, SortOrder=17 },
                new() { JurisdictionId=j["AU"].Id, Code="Var-Minor",   Name="Minor Variation",                                    Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=null, SortOrder=18 },
                new() { JurisdictionId=j["AU"].Id, Code="Var-Notif",   Name="Administrative Notification",                        Category=RIApplicationCategory.Notification,     RequiresApproval=false, ClockDays=null, SortOrder=19 },
                new() { JurisdictionId=j["AU"].Id, Code="USR-AU",      Name="Urgent Safety Restriction",                          Category=RIApplicationCategory.Variation,        RequiresApproval=false, ClockDays=null, SortOrder=20 },

                // ═══ NEW ZEALAND (Medsafe) ══════════════════════════════════════════
                new() { JurisdictionId=j["NZ"].Id, Code="NMA-H",       Name="New Higher-Risk Medicine Application (NMA-H)",        Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=1  },
                new() { JurisdictionId=j["NZ"].Id, Code="NMA-I",       Name="New Intermediate-Risk Medicine Application (NMA-I)",  Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=2  },
                new() { JurisdictionId=j["NZ"].Id, Code="NMA-L",       Name="New Lower-Risk Medicine Application (NMA-L)",         Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=3  },
                new() { JurisdictionId=j["NZ"].Id, Code="Abbrev-NZ",   Name="Abbreviated Evaluation (Reliance on TGA/FDA/EMA)",    Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=4  },
                new() { JurisdictionId=j["NZ"].Id, Code="Verif-2025",  Name="Verification Pathway (Medicines Amendment Act 2025)", Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=30,   SortOrder=5  },
                new() { JurisdictionId=j["NZ"].Id, Code="Prov-NZ",     Name="Provisional Consent",                                Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=6  },
                new() { JurisdictionId=j["NZ"].Id, Code="CMN",         Name="Changed Medicine Notification (CMN)",                Category=RIApplicationCategory.Variation,        RequiresApproval=false, ClockDays=null, SortOrder=7  },
                new() { JurisdictionId=j["NZ"].Id, Code="CMA",         Name="Changed Medicine Application (CMA)",                 Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=null, SortOrder=8  },
                new() { JurisdictionId=j["NZ"].Id, Code="S29",         Name="Section 29 — Named Patient Access",                  Category=RIApplicationCategory.AccessPathway,    RequiresApproval=false, ClockDays=null, SortOrder=9  },

                // ═══ EUROPEAN UNION (EMA) ═══════════════════════════════════════════
                new() { JurisdictionId=j["EU"].Id, Code="CP-Full",     Name="Centralised Procedure — Full MAA",                    Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=210,  SortOrder=1  },
                new() { JurisdictionId=j["EU"].Id, Code="CP-Hybrid",   Name="Centralised Procedure — Hybrid Art.10(3)",            Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=210,  SortOrder=2  },
                new() { JurisdictionId=j["EU"].Id, Code="CP-Generic",  Name="Centralised Procedure — Generic Art.10(1)",           Category=RIApplicationCategory.Generic,          RequiresApproval=true,  ClockDays=210,  SortOrder=3  },
                new() { JurisdictionId=j["EU"].Id, Code="CP-Biosim",   Name="Centralised Procedure — Biosimilar",                  Category=RIApplicationCategory.Biosimilar,       RequiresApproval=true,  ClockDays=210,  SortOrder=4  },
                new() { JurisdictionId=j["EU"].Id, Code="CMA-EU",      Name="Conditional Marketing Authorisation",                 Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=210,  SortOrder=5  },
                new() { JurisdictionId=j["EU"].Id, Code="MA-Exc",      Name="MA Under Exceptional Circumstances",                  Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=210,  SortOrder=6  },
                new() { JurisdictionId=j["EU"].Id, Code="Accel",       Name="Accelerated Assessment",                              Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=150,  SortOrder=7  },
                new() { JurisdictionId=j["EU"].Id, Code="PRIME",       Name="PRIME — Priority Medicines Scheme",                   Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=null, SortOrder=8  },
                new() { JurisdictionId=j["EU"].Id, Code="OD-EU",       Name="Orphan Designation",                                  Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=null, SortOrder=9  },
                new() { JurisdictionId=j["EU"].Id, Code="DCP",         Name="Decentralised Procedure (DCP)",                       Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=210,  SortOrder=10 },
                new() { JurisdictionId=j["EU"].Id, Code="MRP",         Name="Mutual Recognition Procedure (MRP)",                  Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=90,   SortOrder=11 },
                new() { JurisdictionId=j["EU"].Id, Code="Var-IA",      Name="Type IA Variation (immediate notification)",          Category=RIApplicationCategory.Variation,        RequiresApproval=false, ClockDays=null, SortOrder=12 },
                new() { JurisdictionId=j["EU"].Id, Code="Var-IAIN",    Name="Type IAIN Variation",                                 Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=30,   SortOrder=13 },
                new() { JurisdictionId=j["EU"].Id, Code="Var-IB",      Name="Type IB Variation",                                   Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=30,   SortOrder=14 },
                new() { JurisdictionId=j["EU"].Id, Code="Var-II",      Name="Type II Major Variation",                             Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=60,   SortOrder=15 },
                new() { JurisdictionId=j["EU"].Id, Code="Extension",   Name="Extension of Marketing Authorisation",                Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=210,  SortOrder=16 },
                new() { JurisdictionId=j["EU"].Id, Code="USR-EU",      Name="Urgent Safety Restriction",                           Category=RIApplicationCategory.Variation,        RequiresApproval=false, ClockDays=null, SortOrder=17 },

                // ═══ UNITED STATES (FDA) ════════════════════════════════════════════
                new() { JurisdictionId=j["US"].Id, Code="IND",         Name="Investigational New Drug (IND)",                      Category=RIApplicationCategory.ClinicalTrial,    RequiresApproval=false, ClockDays=30,   SortOrder=1  },
                new() { JurisdictionId=j["US"].Id, Code="NDA-505b1",   Name="NDA 505(b)(1) — Full Application",                    Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=300,  SortOrder=2  },
                new() { JurisdictionId=j["US"].Id, Code="NDA-505b2",   Name="NDA 505(b)(2) — Hybrid / Reliance",                   Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=300,  SortOrder=3  },
                new() { JurisdictionId=j["US"].Id, Code="ANDA",        Name="ANDA 505(j) — Generic Drug",                          Category=RIApplicationCategory.Generic,          RequiresApproval=true,  ClockDays=365,  SortOrder=4  },
                new() { JurisdictionId=j["US"].Id, Code="BLA",         Name="Biologics License Application (BLA)",                 Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=365,  SortOrder=5  },
                new() { JurisdictionId=j["US"].Id, Code="BLA-Biosim",  Name="Biosimilar BLA 351(k)",                               Category=RIApplicationCategory.Biosimilar,       RequiresApproval=true,  ClockDays=365,  SortOrder=6  },
                new() { JurisdictionId=j["US"].Id, Code="sNDA",        Name="Supplemental NDA / sBLA",                             Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=180,  SortOrder=7  },
                new() { JurisdictionId=j["US"].Id, Code="PAS",         Name="Prior Approval Supplement (PAS)",                     Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=180,  SortOrder=8  },
                new() { JurisdictionId=j["US"].Id, Code="CBE-30",      Name="Changes Being Effected — 30 Day (CBE-30)",            Category=RIApplicationCategory.Notification,     RequiresApproval=false, ClockDays=30,   SortOrder=9  },
                new() { JurisdictionId=j["US"].Id, Code="ODD-US",      Name="Orphan Drug Designation",                             Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=90,   SortOrder=10 },
                new() { JurisdictionId=j["US"].Id, Code="BTD",         Name="Breakthrough Therapy Designation (BTD)",              Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=60,   SortOrder=11 },
                new() { JurisdictionId=j["US"].Id, Code="FTD",         Name="Fast Track Designation",                              Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=60,   SortOrder=12 },
                new() { JurisdictionId=j["US"].Id, Code="PriorityRev", Name="Priority Review Designation",                         Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=60,   SortOrder=13 },
                new() { JurisdictionId=j["US"].Id, Code="AccelAppr",   Name="Accelerated Approval",                                Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=null, SortOrder=14 },
                new() { JurisdictionId=j["US"].Id, Code="RMAT",        Name="Regenerative Medicine Advanced Therapy (RMAT)",       Category=RIApplicationCategory.Designation,      RequiresApproval=true,  ClockDays=60,   SortOrder=15 },
                new() { JurisdictionId=j["US"].Id, Code="ExpAccess",   Name="Expanded Access (Compassionate Use)",                 Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=30,   SortOrder=16 },

                // ═══ CANADA (Health Canada) ═════════════════════════════════════════
                new() { JurisdictionId=j["CA"].Id, Code="NDS",          Name="New Drug Submission (NDS)",                           Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=300,  SortOrder=1  },
                new() { JurisdictionId=j["CA"].Id, Code="SNDS",         Name="Supplemental NDS (SNDS)",                             Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=300,  SortOrder=2  },
                new() { JurisdictionId=j["CA"].Id, Code="ANDS",         Name="Abbreviated NDS — Generic (ANDS)",                    Category=RIApplicationCategory.Generic,          RequiresApproval=true,  ClockDays=300,  SortOrder=3  },
                new() { JurisdictionId=j["CA"].Id, Code="SANDS",        Name="Supplemental ANDS (SANDS)",                           Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=300,  SortOrder=4  },
                new() { JurisdictionId=j["CA"].Id, Code="EUNDS",        Name="Extraordinary Use New Drug Submission (EUNDS)",       Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=300,  SortOrder=5  },
                new() { JurisdictionId=j["CA"].Id, Code="NDS-Priority", Name="NDS — Priority Review",                               Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=180,  SortOrder=6  },
                new() { JurisdictionId=j["CA"].Id, Code="NOCc",         Name="Notice of Compliance with Conditions (NOC/c)",        Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=7  },
                new() { JurisdictionId=j["CA"].Id, Code="SAP",          Name="Special Access Programme (SAP)",                      Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=8  },
                new() { JurisdictionId=j["CA"].Id, Code="PDC-Q",        Name="Post-NOC Changes — Quality (PDC-Q)",                  Category=RIApplicationCategory.Variation,        RequiresApproval=true,  ClockDays=null, SortOrder=9  },

                // ═══ SINGAPORE (HSA) ════════════════════════════════════════════════
                new() { JurisdictionId=j["SG"].Id, Code="NDA-1",        Name="New Drug Application — NDA-1 (Full Evaluation)",      Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=1  },
                new() { JurisdictionId=j["SG"].Id, Code="NDA-2",        Name="New Drug Application — NDA-2 (Abridged)",             Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=2  },
                new() { JurisdictionId=j["SG"].Id, Code="NDA-3",        Name="New Drug Application — NDA-3 (Verification)",         Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=3  },
                new() { JurisdictionId=j["SG"].Id, Code="GDA-1",        Name="Generic Drug Application — GDA-1",                    Category=RIApplicationCategory.Generic,          RequiresApproval=true,  ClockDays=null, SortOrder=4  },
                new() { JurisdictionId=j["SG"].Id, Code="GDA-2",        Name="Generic Drug Application — GDA-2",                    Category=RIApplicationCategory.Generic,          RequiresApproval=true,  ClockDays=null, SortOrder=5  },
                new() { JurisdictionId=j["SG"].Id, Code="PRIME-SG",     Name="HSA PRIME Scheme — Rare/Serious (120–180 days)",      Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=150,  SortOrder=6  },
                new() { JurisdictionId=j["SG"].Id, Code="SAR",          Name="Special Access Route (SAR)",                          Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=7  },
                new() { JurisdictionId=j["SG"].Id, Code="Abridged-SG",  Name="Abridged Registration (TGA/FDA/EMA reference)",       Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=8  },

                // ═══ MALAYSIA (NPRA) ════════════════════════════════════════════════
                new() { JurisdictionId=j["MY"].Id, Code="Full-MY",      Name="Full Evaluation Registration",                        Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=1  },
                new() { JurisdictionId=j["MY"].Id, Code="Abridged-MY",  Name="Abridged Evaluation Registration (Reference Country)",Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=2  },
                new() { JurisdictionId=j["MY"].Id, Code="Prov-MY",      Name="Provisional Registration (Urgent Access)",            Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=3  },

                // ═══ BRAZIL (ANVISA) ════════════════════════════════════════════════
                new() { JurisdictionId=j["BR"].Id, Code="RegNew-BR",    Name="New Drug Registration",                               Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=1  },
                new() { JurisdictionId=j["BR"].Id, Code="Biosim-BR",    Name="Biosimilar Registration (RDC 875/2024)",              Category=RIApplicationCategory.Biosimilar,       RequiresApproval=true,  ClockDays=null, SortOrder=2  },
                new() { JurisdictionId=j["BR"].Id, Code="Reliance-BR",  Name="Reliance Pathway (RDC 868/2024 — EMA/FDA/TGA basis)", Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=3  },
                new() { JurisdictionId=j["BR"].Id, Code="Comps-BR",     Name="Compassionate Use",                                   Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=4  },

                // ═══ MEXICO (COFEPRIS) ══════════════════════════════════════════════
                new() { JurisdictionId=j["MX"].Id, Code="SanReg-MX",   Name="Sanitary Registration (Standard)",                    Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=1  },
                new() { JurisdictionId=j["MX"].Id, Code="Equiv-MX",    Name="Abbreviated Equivalence Pathway (Sept 2025 — 30 wd)", Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=30,   SortOrder=2  },
                new() { JurisdictionId=j["MX"].Id, Code="ATP-MX",      Name="Third-Party Pre-Assessment (ATP) Review",             Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=3  },

                // ═══ COLOMBIA (INVIMA) ══════════════════════════════════════════════
                new() { JurisdictionId=j["CO"].Id, Code="RegCtrl-CO",  Name="Sanitary Registry — Controlled",                      Category=RIApplicationCategory.FullRegistration, RequiresApproval=true,  ClockDays=null, SortOrder=1  },
                new() { JurisdictionId=j["CO"].Id, Code="RegAuto-CO",  Name="Sanitary Registry — Automatic Notification",          Category=RIApplicationCategory.Notification,     RequiresApproval=false, ClockDays=null, SortOrder=2  },
                new() { JurisdictionId=j["CO"].Id, Code="EUA-CO",      Name="Emergency Use Authorisation",                         Category=RIApplicationCategory.AccessPathway,    RequiresApproval=true,  ClockDays=null, SortOrder=3  },
            };

            context.RIApplicationTypes.AddRange(types);
            context.SaveChanges();
            } // end if !RIApplicationTypes.Any()
        } // end Seed()
    }
}
