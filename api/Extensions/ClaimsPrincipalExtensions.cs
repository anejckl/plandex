using System.Security.Claims;

namespace Plandex.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub")
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.Identity?.Name;
        if (sub is null || !int.TryParse(sub, out var id))
            throw new UnauthorizedAccessException("Missing user id claim");
        return id;
    }
}
