using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpsSecProject.Models
{
    public class Role
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Display(Name = "Role")]
        [Required]
        public string RoleName { get; set; }
        [Required]
        public string IDPReference { get; set; }

        public virtual ICollection<User> Users { get; set; }
    }
}
