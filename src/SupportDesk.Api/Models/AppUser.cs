using Microsoft.AspNetCore.Identity;
namespace SupportDesk.Api.Models;
public sealed class AppUser : IdentityUser { }
public sealed record Credentials(string Email, string Password);
public sealed record RoleRequest(string Role);
