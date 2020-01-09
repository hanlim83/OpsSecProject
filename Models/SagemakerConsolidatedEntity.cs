using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace OpsSecProject.Models
{
    public enum SagemakerStatus
    {
        Untrained, Training, Tuning, Deploying, Ready
    }
    public class SagemakerConsolidatedEntity
    {
        public int ID { get; set; }
        public SagemakerStatus SagemakerStatus { get; set; }
        public string SagemakerReference { get; set; }
        public string SagemakerEndpoint { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
