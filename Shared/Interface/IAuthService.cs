using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Models.AuthUser;

namespace Shared.Interface
{
    public interface IAuthService
    {
         Task<string> LoginAsync(LoginModel model);
        Task<bool> RegisterAsync(UserModel model);
    }
}