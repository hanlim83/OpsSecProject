using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class LogInput
    {
        public int ID { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string FilePath { get; set; }
        // public string Pattern {get; set;}
        // public string {get; set;}
        [Required]
        public string LinkedUserName { get; set; }

    }
}
