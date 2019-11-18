using OpsSecProject.Models;
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
                context.Add(new Role
                {
                    RoleName = "Power User",
                    IDPReference = "9eb7a8cb-db12-4f3e-bbb3-f4576868b3ec"
                });
                context.Add(new Role
                {
                    RoleName = "Administrator",
                    IDPReference = "8325c997-07db-4297-8f1e-e2c8b506e30"
                });
                context.SaveChanges();
            }
            if (!context.Users.Any())
            {
                context.Add(new User
                {
                    Username = "Admin",
                    Password = Password.HashPassword("SmartInsights",Password.GetRandomSalt()),
                    PhoneNumber = "97931442",
                    VerifiedPhoneNumber = true,
                    LinkedRole = context.Roles.Find("Administrator"),
                    Existence = Existence.Internal 
                });
                context.SaveChanges();
            }
        }
    }
}
