﻿using OpsSecProject.Models;
using System.Collections.Generic;

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
        public List<ApacheWebLog> cardsTotalIps { get; set; }
        public List<ApacheWebLog> cardsTotalBytes { get; set; }       
        public List<ApacheWebLog> cardsTopIp { get; set; }
        
        public List<ApacheWebLog> chartsBarReq { get; set; }


        // SSH stuff
        public List<SSHServerLogs> sshlogs { get; set; }
        public List<SSHServerLogs> SSHresults { get; set; }
        public List<SSHServerLogs> cardsFailedLogin { get; set; }
        public List<SSHServerLogs> cardsTopUserFailedLogin { get; set; }
        public List<SSHServerLogs> cardsTopPort { get; set; }
        
        public List<SSHServerLogs> chartsPieLogin { get; set; }        
        public List<SSHServerLogs> chartsBarLoginAttemptsTime { get; set; }
        public List<SSHServerLogs> chartsBarLoginAttemptsTime2 { get; set; }

        // Squid stuff
        public List<SquidProxyLog> squidlogs { get; set; }
        public List<SquidProxyLog> squidResults { get; set; }
        public List<SquidProxyLog> cardsTopContentType { get; set; }
        public List<SquidProxyLog> cardsTopDestIp { get; set; }
        public List<SquidProxyLog> cardsTopReqUser { get; set; }


        //Windows stuff
        public List<WindowsSecurityLog> windowslogs { get; set; }
        public List<WindowsSecurityLog> cardsFailedAccount { get; set; }

        public Trigger sagemakerConsolidatedEntity { get; set; }
    }
}
