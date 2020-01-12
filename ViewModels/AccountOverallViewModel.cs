using OpsSecProject.Models;
using System.Collections.Generic;

namespace OpsSecProject.ViewModels
{
    public class AccountOverallViewModel
    {
        public User User { get; set; }
        public List<Activity> Useractivites { get; set; }

        public Settings UserSettings { get; set; }
    }
}
