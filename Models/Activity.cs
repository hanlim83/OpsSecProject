using System;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum Action
    {
        View, Edit
    }
    public class Activity
    {
        public int ID { get; set; }
        [Required]
        public DateTime Timestamp { get; set; }
        [Required]
        public string Page { get; set; }
        [Required]
        public Action Action { get; set; }
        [Required]
        public bool Status { get; set; }
        [Required]
        public int LinkedUserID { get; set; }
        [Required]
        public virtual User LinkedUser { get; set; }
    }
}
