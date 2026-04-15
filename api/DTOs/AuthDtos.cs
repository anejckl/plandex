using System.ComponentModel.DataAnnotations;

namespace Plandex.Api.DTOs;

public record RegisterDto(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(200, MinimumLength = 8)] string Password,
    [Required, StringLength(200)] string Name);

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record UserDto(int Id, string Email, string Name);

public record AuthResponseDto(string AccessToken, UserDto User);
