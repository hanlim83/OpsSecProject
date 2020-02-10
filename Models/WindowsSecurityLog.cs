using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public class WindowsSecurityLog
    {
        public int eventid { get; set; }
        public string description { get; set; }
        public string leveldisplayname { get; set; }
        public string logname { get; set; }
        public string machinename { get; set; }
        public string providername { get; set; }
        public string timecreated { get; set; }
        public int index { get; set; }
        public string username { get; set; }
        public string keywords { get; set; }
        public string eventdata { get; set; }

        // Cards
        public string failedAccount { get; set; }
        public string totalNumFailedAccount { get; set; }

    }


}
