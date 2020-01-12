using OpsSecProject.Models;
using System.Collections.Generic;

namespace OpsSecProject.ViewModels
{
    public class UsersOverallManagementViewModel
    {
        public List<User> allUsers { get; set; }
        public List<Role> allRoles { get; set; }
    }
}
