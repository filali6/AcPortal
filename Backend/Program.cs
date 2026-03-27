using Backend.Data;
using Backend.Kafka;
using Backend.Modules.Auth.Services;
using Backend.Modules.Events.Services;
using Backend.Modules.Projects.Services;
using Backend.Modules.Tools.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Backend.Modules.Tasks.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
 
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration
        .GetConnectionString("DefaultConnection")));

 
builder.Services.AddHostedService<KafkaConsumerService>();
//builder.Services.AddSingleton<KafkaProducerService>();

builder.Services.AddHostedService<OutboxPublisherService>();
 
builder.Services.AddScoped<EventProcessorService>();
builder.Services.AddScoped<TasksService>();
builder.Services.AddScoped<EventsService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<ProjectsService>();
builder.Services.AddScoped<TeamsService>();
builder.Services.AddScoped<ToolsService>();


builder.Services.AddControllers();
var secretKey = builder.Configuration["Jwt:SecretKey"]!;
var issuer = builder.Configuration["Jwt:Issuer"]!;
var audience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(
                                       Encoding.UTF8.GetBytes(secretKey))
    };
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});




var app = builder.Build();

 
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}




app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();