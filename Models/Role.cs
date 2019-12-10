using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class Role
    {
        public int ID { get; set; }
        [Display(Name = "Role")]
        [Required]
        public string RoleName { get; set; }
        [Required]
        public Existence Existence { get; set; }
        public string IDPReference { get; set; }

        public virtual ICollection<User> Users { get; set; }
    }
}
