using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Models.AuthUser
{
    public class LoginModel
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
    }
}