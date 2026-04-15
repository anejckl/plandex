using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Plandex.Api.Models;

namespace Plandex.Api.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    (string raw, string hash) GenerateRefreshToken();
    string HashRefreshToken(string raw);
    int AccessMinutes { get; }
    int RefreshDays { get; }
}

public class JwtTokenService : ITokenService
{
    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    public int AccessMinutes { get; }
    public int RefreshDays { get; }

    public JwtTokenService(IConfiguration cfg)
    {
        var secret = cfg["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret missing");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _issuer = cfg["Jwt:Issuer"] ?? "plandex";
        AccessMinutes = int.Parse(cfg["Jwt:AccessMinutes"] ?? "15");
        RefreshDays = int.Parse(cfg["Jwt:RefreshDays"] ?? "7");
    }

    public string GenerateAccessToken(User user)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name)
        };
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AccessMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string raw, string hash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var raw = Convert.ToBase64String(bytes);
        return (raw, HashRefreshToken(raw));
    }

    public string HashRefreshToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
