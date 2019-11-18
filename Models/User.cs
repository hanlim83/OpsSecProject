using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpsSecProject.Models
{
    public enum Existence
    {
        Internal,External,Hybrid
    }
    public class User
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Username { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; }
        [Display(Name = "Email Address")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }
        [Required]
        public Existence Existence { get; set; }
        public DateTime LastSignedIn { get; set; }
        public bool ForceSignOut { get; set; }
        public bool VerifiedEmail { get; set; }
        public bool VerifiedPhoneNumber { get; set; }
        public string IDPReference { get; set; }

        [Required]
        public virtual Role LinkedRole { get; set; }
        public virtual ICollection<NotificationToken> NotificationTokens { get; set; }
    }
}
