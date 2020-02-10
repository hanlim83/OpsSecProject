namespace OpsSecProject.Models
{
    public class SSHServerLogs
    {
        public string weekday { get; set; }
        public string month { get; set; }
        public string day { get; set; }
        public string year { get; set; }
        public string time { get; set; }
        public string host { get; set; }
        public string process { get; set; }
        public string identifier { get; set; }
        public string message { get; set; }

        // Cards
        public string COUNT { get; set; }
        public string failedLogin { get; set; }
        public string u { get; set; }
        public string totalNum { get; set; }
        public string topPort { get; set; }
        public string totalNumPort { get; set; }


        // Pie Chart Login Attempt
        public string loginAttempt { get; set; }
        public string totalNumLoginAttempt { get; set; }

    }
}