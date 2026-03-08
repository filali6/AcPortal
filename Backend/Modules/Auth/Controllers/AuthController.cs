using Backend.Modules.Auth.Models;
using Backend.Modules.Auth.Services;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Backend.Modules.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    public AuthController(AuthService authService)
    {
        _authService=authService;
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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token=await _authService.LoginAsync(request.Email,request.Password);
        if (token ==null)
            return Unauthorized(new {message="wrong email or password"}) ;
        return Ok(new
        {
            message="login success",
            token=token
        });

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
    public class LoginRequest
    {
       [Required(ErrorMessage ="email is required")]
       public string Email{get;set;}=string.Empty;

       [Required(ErrorMessage ="password is required")]
       public string Password {get;set;}=string.Empty; 
    }
}