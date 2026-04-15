using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plandex.Api.Data;
using Plandex.Api.DTOs;
using Plandex.Api.Extensions;
using Plandex.Api.Models;
using Plandex.Api.Services;

namespace Plandex.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PlandexDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    private const string RefreshCookieName = "plandex_refresh";

    public AuthController(PlandexDbContext db, IPasswordHasher hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { error = "Email already registered" });

        var user = new User
        {
            Email = email,
            Name = dto.Name.Trim(),
            PasswordHash = _hasher.Hash(dto.Password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(await IssueAsync(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !_hasher.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        return Ok(await IssueAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var raw) || string.IsNullOrEmpty(raw))
            return Unauthorized(new { error = "No refresh token" });

        var hash = _tokens.HashRefreshToken(raw);
        var token = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (token is null || token.Revoked || token.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = "Invalid refresh token" });

        token.Revoked = true;
        await _db.SaveChangesAsync();

        return Ok(await IssueAsync(token.User));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var raw) && !string.IsNullOrEmpty(raw))
        {
            var hash = _tokens.HashRefreshToken(raw);
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
            if (token is not null)
            {
                token.Revoked = true;
                await _db.SaveChangesAsync();
            }
        }
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();
        return new UserDto(user.Id, user.Email, user.Name);
    }

    private async Task<AuthResponseDto> IssueAsync(User user)
    {
        var access = _tokens.GenerateAccessToken(user);
        var (raw, hash) = _tokens.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_tokens.RefreshDays)
        });
        await _db.SaveChangesAsync();

        Response.Cookies.Append(RefreshCookieName, raw, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(_tokens.RefreshDays)
        });

        return new AuthResponseDto(access, new UserDto(user.Id, user.Email, user.Name));
    }
}
