using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Auth.Services;
 
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Modules.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AppDbContext _db;
    public AuthController(AuthService authService,AppDbContext db)
    {
        _authService=authService;
        _db=db;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user= await _authService.RegisterAsync(
            request.FullName,request.Email,request.Password,request.Role

        );
        if (user==null)
           return BadRequest(new{message="email already user"});
        
        return Ok(new
        {
            message = " account created successfuly",
            date = new
            {
                id=user.Id,
                fullName=user.FullName,
                email=user.Email,
                role=user.Role.ToString()
            }
        });
    }
 
    [HttpGet("users")]
    [Authorize(Roles ="HeadOfCDS,PortfolioDirector,ProjectManager")]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Select(u => new
            {
                id = u.Id,
                fullName = u.FullName,
                email = u.Email,
                role = u.Role.ToString()
            })
            .ToListAsync();

        return Ok(users);
    }
    [HttpGet("users/project-managers")]
    [Authorize]
    public async Task<IActionResult> GetProjectManagers()
    {
        var managers = await _db.Users
            .Where(u => u.Role == GlobalRole.ProjectManager)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                ProjectCount = _db.Projects
                    .Count(p => p.ProjectManagerId == u.Id)
            })
            .ToListAsync();
        return Ok(managers);
    }
    // Dans AuthController — ajoute cet endpoint
    [HttpGet("users/leads")]
    [Authorize]
    public async Task<IActionResult> GetLeads()
    {
        var leads = await _db.Users
            .Where(u => u.Role == GlobalRole.BusinessTeamLead
                     || u.Role == GlobalRole.TechnicalTeamLead)
            .Select(u => new
            {
                id = u.Id,
                fullName = u.FullName,
                email = u.Email,
                role = u.Role.ToString(),
                StreamCount = _db.Streams
                    .Count(s => s.BusinessTeamLeadId == u.Id
                             || s.TechnicalTeamLeadId == u.Id)
            })
            .ToListAsync();
        return Ok(leads);
    }
    public class RegisterRequest
    {
        [Required(ErrorMessage ="FullName is required")]
        public string FullName{get;set;}=string.Empty;

        [Required(ErrorMessage ="Email is required")]
        [EmailAddress(ErrorMessage ="invalid email")]
        public string Email{get;set;}=string.Empty;

        [Required(ErrorMessage ="password is required")]
        [MinLength(6,ErrorMessage ="password needs to have at least 6 carac")]
        public string Password{get;set;}=string.Empty;
        public GlobalRole Role{get;set;}=GlobalRole.Consultant;
    }
   
    
}