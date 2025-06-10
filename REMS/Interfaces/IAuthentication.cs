using REMS.Abstractions;
using REMS.Enititys;
using System.Security.Claims;

namespace REMS.Interfaces;

public interface IAuthentication
{
    public Task<Result<ClaimsPrincipal>> Login(string email, string password);
}
