using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using SupportDesk.Api.Data;
using SupportDesk.Api.Models;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("SupportDesk")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:SupportDesk before starting.");
builder.Services.AddDbContext<SupportDeskDb>(options => options.UseSqlServer(connection));
var secret = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Configure Jwt:Key.");
if (Encoding.UTF8.GetByteCount(secret) < 32) throw new InvalidOperationException("JWT key must be at least 32 bytes.");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
builder.Services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<SupportDeskDb>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "SupportDesk",
        ValidateAudience = true, ValidAudience = "SupportDesk",
        ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key,
        NameClaimType = "sub", RoleClaimType = "role", ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
var app = builder.Build();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Temporary local development bootstrap until migrations are introduced.
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SupportDeskDb>().Database.EnsureCreatedAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Employee", "Agent", "Admin" })
        if (!await roles.RoleExistsAsync(role) && !(await roles.CreateAsync(new IdentityRole(role))).Succeeded)
            throw new InvalidOperationException("Could not create role.");
    var email = builder.Configuration["Bootstrap:Email"];
    var password = builder.Configuration["Bootstrap:Password"];
    if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByNameAsync(email) is null)
        {
            var admin = new AppUser { UserName = email, Email = email };
            if (!(await users.CreateAsync(admin, password)).Succeeded || !(await users.AddToRoleAsync(admin, "Admin")).Succeeded)
                throw new InvalidOperationException("Could not create bootstrap admin.");
        }
    }
}
app.MapGet("/health", async (SupportDeskDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapPost("/api/auth/register", async (Credentials request, UserManager<AppUser> users) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) ||
        request.Email.Length > 254 || request.Password.Length > 128 ||
        !System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
        return Results.BadRequest(new { error = "Invalid credentials." });
    var user = new AppUser { UserName = request.Email, Email = request.Email };
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded) return Results.BadRequest(new { error = "Registration failed." });
    if (!(await users.AddToRoleAsync(user, "Employee")).Succeeded)
    {
        await users.DeleteAsync(user);
        return Results.Problem("Registration failed.");
    }
    return Results.Created("/api/auth/login", new { message = "Account created." });
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async (Credentials request, UserManager<AppUser> users) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
        return Results.Unauthorized();
    var user = await users.FindByNameAsync(request.Email);
    if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
    var roles = await users.GetRolesAsync(user);
    var claims = new List<Claim> { new("sub", user.Id) };
    claims.AddRange(roles.Select(role => new Claim("role", role)));
    var token = new JwtSecurityToken("SupportDesk", "SupportDesk", claims,
        expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), roles });
}).RequireRateLimiting("auth");
app.MapPut("/api/users/{id}/role", async (string id, RoleRequest request, UserManager<AppUser> users, ClaimsPrincipal caller) =>
{
    if (id == caller.FindFirstValue("sub") || request.Role is not ("Employee" or "Agent")) return Results.BadRequest();
    var user = await users.FindByIdAsync(id);
    if (user is null) return Results.NotFound();
    var current = await users.GetRolesAsync(user);
    if (current.Contains("Admin")) return Results.Forbid();
    if (!(await users.RemoveFromRolesAsync(user, current)).Succeeded) return Results.Problem();
    if (!(await users.AddToRoleAsync(user, request.Role)).Succeeded) return Results.Problem();
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/tickets", async (SupportDeskDb db, ClaimsPrincipal caller, CancellationToken ct) =>
    {
    var owner = caller.FindFirstValue("sub");
    var privileged = caller.IsInRole("Admin") || caller.IsInRole("Agent");
    return await db.Tickets.AsNoTracking().Where(t => privileged || t.OwnerId == owner).OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
});
api.MapGet("/tickets/{id:guid}", async (Guid id, SupportDeskDb db, ClaimsPrincipal caller, CancellationToken ct) =>
{
    var ticket = await db.Tickets.FindAsync(new object[] { id }, ct);
    return ticket is not null && CanAccess(ticket, caller) ? Results.Ok(ticket) : Results.NotFound();
});
api.MapPost("/tickets", async (CreateTicket request, SupportDeskDb db, ClaimsPrincipal caller, CancellationToken ct) =>
{
    if (!Valid(request.Title, request.Description)) return Results.BadRequest(new { error = "Title (1-120) and description (1-4000) are required." });
    var ticket = new Ticket(Guid.NewGuid(), request.Title.Trim(), request.Description.Trim(), "Open", DateTimeOffset.UtcNow, caller.FindFirstValue("sub")!);
    db.Tickets.Add(ticket);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/tickets/{ticket.Id}", ticket);
});
api.MapPut("/tickets/{id:guid}", async (Guid id, UpdateTicket request, SupportDeskDb db, ClaimsPrincipal caller, CancellationToken ct) =>
{
    if (!Valid(request.Title, request.Description) || request.Status is not ("Open" or "InProgress" or "Resolved"))
        return Results.BadRequest(new { error = "Invalid title, description or status." });
    var ticket = await db.Tickets.FindAsync(new object[] { id }, ct);
    if (ticket is null || !CanAccess(ticket, caller)) return Results.NotFound();
    if (!caller.IsInRole("Admin") && !caller.IsInRole("Agent") && request.Status != ticket.Status) return Results.Forbid();
    db.Entry(ticket).CurrentValues.SetValues(ticket with { Title = request.Title.Trim(), Description = request.Description.Trim(), Status = request.Status });
    await db.SaveChangesAsync(ct);
    return Results.Ok(ticket);
});
api.MapDelete("/tickets/{id:guid}", async (Guid id, SupportDeskDb db, CancellationToken ct) =>
{
    var ticket = await db.Tickets.FindAsync(new object[] { id }, ct);
    if (ticket is null) return Results.NotFound();
    db.Tickets.Remove(ticket);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin"));
app.Run();

static bool CanAccess(Ticket ticket, ClaimsPrincipal caller) => caller.IsInRole("Admin") || caller.IsInRole("Agent") || ticket.OwnerId == caller.FindFirstValue("sub");

static bool Valid(string? title, string? description) =>
    !string.IsNullOrWhiteSpace(title) && title.Length <= 120 &&
    !string.IsNullOrWhiteSpace(description) && description.Length <= 4000;
