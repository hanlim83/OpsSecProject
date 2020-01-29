using OpsSecProject.Models;
using System.Collections.Generic;

namespace OpsSecProject.ViewModels
{
    public class InputsOverrallViewModel
    {
        public List<LogInput> inputs { get; set; }
        public List<User> allUsers { get; set; }
        public User currentUser { get; set; }
    }
}
