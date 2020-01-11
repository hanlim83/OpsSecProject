using OpsSecProject.Models;
using System.Collections.Generic;

namespace OpsSecProject.ViewModels
{
    public class AlertsViewModel
    {
        public List<Alert> allAlerts { get; set; }
        public int informationalAlerts { get; set; }
        public int warningAlerts { get; set; }
        public int dangerAlerts { get; set; }
        public int successAlerts { get; set; }
    }
}
