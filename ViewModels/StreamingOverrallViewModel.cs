using OpsSecProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.ViewModels
{
    public class StreamingOverrallViewModel
    {
        public LogInput input { get; set; }

        // Apache stuff
        public List<ApacheWebLog> webLogs { get; set; }
        public List<ApacheWebLog> results { get; set; }
        public List<ApacheWebLog> charts { get; set; }
        public List<ApacheWebLog> count { get; set; }
        public List<ApacheWebLog> groupBy { get; set; }
        // SSH stuff
        public List<SSHServerLogs> sshlogs { get; set; }
        public List<SSHServerLogs> SSHresults { get; set; }
        // Squid stuff
        public List<SquidProxyLog> squidlogs { get; set; }
        //Windows stuff
        public List<WindowsSecurityLog> windowslogs { get; set; }
        public SagemakerConsolidatedEntity sagemakerConsolidatedEntity { get; set; }
    }
}
