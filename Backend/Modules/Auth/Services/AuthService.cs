using Backend.Data;
using Backend.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Backend.Modules.Auth.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService>_logger ; 

    public AuthService(AppDbContext db,IConfiguration configuration,ILogger<AuthService> logger)
    {
        _db=db;
        _configuration=configuration;
        _logger=logger;
    }
    public async Task<User?> RegisterAsync(string fullName,string email, string password , GlobalRole role)
    {
        var exists=await _db.Users.AnyAsync(u=>u.Email==email);
        if (exists)
        {
            _logger.LogWarning("cet email existe deja :{Email}",email);
            return null;
        }
        var user= new User
        {
            FullName=fullName,
            Email=email,
            PasswordHash=BCrypt.Net.BCrypt.HashPassword(password),
            Role=role,
            CreatedAt=DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("new user created:{Email}/{Role}",email,role);
        return user ;
    }
    public async Task<string?> LoginAsync(string email, string password)
    {
        var user= await _db.Users.FirstOrDefaultAsync(u=>u.Email==email);
        if (user == null)
        {
            _logger.LogWarning("login failed , can't find email:{Email}",email);
            return null ;
        }
        var isValid=BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!isValid)
        {
            _logger.LogWarning("login failed , wrong password : {Email}",email);
            return null;
        }
        return GenerateJwtToken(user);

        
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey=_configuration["jwt:SecretKey"]!;
        var issuer=_configuration["jwt:Issuer"]!;
        var audience=_configuration["jwt:Audience"]!;
        var expHours=int.Parse(_configuration["jwt:ExpirationHours"]!);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds= new SigningCredentials(key,SecurityAlgorithms.HmacSha256);


        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),
            new Claim (ClaimTypes.Email,user.Email),
            new Claim (ClaimTypes.Name,user.FullName),
            new Claim ( ClaimTypes.Role,user.Role.ToString())

        };
        var token = new JwtSecurityToken(issuer:issuer,
        audience:audience,claims:claims,expires:DateTime.UtcNow.AddHours(expHours),
        signingCredentials:creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
 
}