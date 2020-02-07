using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum AlertTriggerType
    {
        CountByTimeStamp, CountAlone
    }
    public class Trigger
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string CondtionalField { get; set; }
        [Required]
        public string Condtion { get; set; }
        [Required]
        public string CondtionType { get; set; }
        [Required]
        public AlertTriggerType AlertTriggerType { get; set; }
        [Required]
        public int LinkedLogInputID { get; set; }
        [Required]
        public virtual LogInput LinkedLogInput { get; set; }
    }
}
