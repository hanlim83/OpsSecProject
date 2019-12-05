﻿using OpsSecProject.Areas.Internal.Data;
using OpsSecProject.Models;
using System;
using System.Linq;

namespace OpsSecProject.Data
{
    public static class DbInitializer
    {
        public static void InitializeAuthenticationContext(AuthenticationContext context)
        {
            context.Database.EnsureCreated();
            if (!context.Roles.Any())
            {
                context.Roles.Add(new Role
                {
                    RoleName = "Power User",
                    Existence = Existence.Hybrid,
                    IDPReference = "9eb7a8cb-db12-4f3e-bbb3-f4576868b3ec"
                });
                context.Roles.Add(new Role
                {
                    RoleName = "Administrator",
                    Existence = Existence.Hybrid,
                    IDPReference = "8325c997-07db-4297-8f1e-e2c8b506e309"
                });
                context.SaveChanges();
            }
            if (!context.Users.Any())
            {
                context.Users.Add(new User
                {
                    Username = "Admin",
                    Name = "Administrator",
                    Password = Password.HashPassword("SmartInsights", Password.GetRandomSalt()),
                    PhoneNumber = "97931442",
                    VerifiedPhoneNumber = true,
                    LinkedRole = context.Roles.Find("Administrator"),
                    Existence = Existence.Internal,
                    Status = Status.Active,
                    OverridableField = OverridableField.Both,
                    LastPasswordChange = DateTime.Now
                });
                context.SaveChanges();
            }
        }
    }
}
