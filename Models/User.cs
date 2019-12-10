using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OpsSecProject.Models
{
    public enum Existence
    {
        Internal,External,Hybrid
    }

    public enum OverridableField
    {
        EmailAddress,PhoneNumber,Both,None
    }

    public enum Status
    {
        Pending,Active,Disabled
    }
    public class User
    {
        public int ID { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        public string PhoneNumber { get; set; }
        public string Name { get; set; }
        [Display(Name = "Email Address")]
        [DataType(DataType.EmailAddress)]
        public string EmailAddress { get; set; }
        [Required]
        public Existence Existence { get; set; }
        public DateTime LastAuthentication { get; set; }
        [Required]
        public DateTime LastPasswordChange { get; set; }
        [Required]
        public bool ForceSignOut { get; set; }
        [Required]
        public bool VerifiedEmailAddress { get; set; }
        [Required]
        public bool VerifiedPhoneNumber { get; set; }
        [Required]
        public Status Status { get; set; }
        [Required]
        public OverridableField OverridableField { get; set; }
        public string IDPReference { get; set; }

        public virtual Role LinkedRole { get; set; }
        public virtual ICollection<NotificationToken> NotificationTokens { get; set; }
    }
}
