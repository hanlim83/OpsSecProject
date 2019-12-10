using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public class AuditOverallViewModel
    {
        public List<Activity> Activites { get; set; }
        public List<User> allUsers { get; set; }
    }
}
