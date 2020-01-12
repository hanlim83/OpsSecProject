using Microsoft.EntityFrameworkCore;
using OpsSecProject.Areas.Internal.Data;
using OpsSecProject.Models;
using System;
using System.Linq;

namespace OpsSecProject.Data
{
    public static class DbInitializer
    {
        public static void InitializeAccountContext(AccountContext context)
        {
            context.Database.EnsureCreated();
            if (!context.Roles.Any())
            {
                context.Database.OpenConnection();
                context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.Roles ON");
                context.Roles.Add(new Role
                {
                    ID = 2,
                    RoleName = "Power User",
                    Existence = Existence.Hybrid,
                    IDPReference = "9eb7a8cb-db12-4f3e-bbb3-f4576868b3ec"
                });
                context.Roles.Add(new Role
                {
                    ID = 1,
                    RoleName = "Administrator",
                    Existence = Existence.Hybrid,
                    IDPReference = "8325c997-07db-4297-8f1e-e2c8b506e309"
                });
                context.SaveChanges();
                context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.Roles OFF");
                context.Database.CloseConnection();
            }
            if (!context.Users.Any())
            {
                context.Database.OpenConnection();
                context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.Users ON");
                context.Users.Add(new User
                {
                    ID = 1,
                    Username = "Admin",
                    Name = "Administrator",
                    Password = Password.HashPassword("SmartInsights", Password.GetRandomSalt()),
                    PhoneNumber = "97931442",
                    VerifiedPhoneNumber = true,
                    LinkedRole = context.Roles.Where(r => r.RoleName.Equals("Administrator")).FirstOrDefault(),
                    Existence = Existence.Internal,
                    Status = UserStatus.Active,
                    OverridableField = OverridableField.Both,
                    LastPasswordChange = DateTime.Now,
                    LastAuthentication = DateTime.Now
                });
                context.SaveChanges();
                context.Database.ExecuteSqlCommand("SET IDENTITY_INSERT dbo.Users OFF");
                context.Settings.Add(new Settings
                {
                    LinkedUserID = 1,
                    LinkedUser = context.Users.Find(1),
                    CommmuicationOptions = CommmuicationOptions.SMS
                });
                context.SaveChanges();
                context.Database.CloseConnection();
            }
        }

        public static void InitializeLogContext(LogContext context)
        {
            context.Database.EnsureCreated();
        }
    }
}
