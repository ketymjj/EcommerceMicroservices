using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared.Models.AuthUser
{
    public class UserModel
    {
           public int Id { get; set; }
           public string Username { get; set; } = string.Empty;
           public string PasswordHash { get; set; } = string.Empty;
           public string Role { get; set; } = "User";
    }
}