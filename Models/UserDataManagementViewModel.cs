using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public class UserDataManagementViewModel
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Name { get; set; }
        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; }
        [Display(Name = "Email Address")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }
        [Required]
        public string Role { get; set; }

        public User user { get; set; }
        public List<Role> allRoles { get; set; }
    }
}
