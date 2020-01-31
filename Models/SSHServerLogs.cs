using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    }
}