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
        public List<ApacheWebLog> webLogs { get; set; }
        public SagemakerConsolidatedEntity sagemakerConsolidatedEntity { get; set; }
    }
}
