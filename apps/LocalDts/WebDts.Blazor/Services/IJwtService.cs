using System.Security.Claims;

namespace WebDts.Blazor.Services;

public interface IJwtService
{
    string GenerateToken(string userId, string userName, IEnumerable<string> roles);
    ClaimsPrincipal? ValidateToken(string token);
}
