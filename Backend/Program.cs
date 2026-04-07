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
using Backend.Modules.Events.Handlers;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Backend.Modules.Auth;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration
        .GetConnectionString("DefaultConnection")),
    ServiceLifetime.Transient);


builder.Services.AddHostedService<KafkaConsumerService>();


//builder.Services.AddSingleton<KafkaProducerService>();

builder.Services.AddHostedService<OutboxPublisherService>();
 
builder.Services.AddScoped<EventProcessorService>();
builder.Services.AddSingleton<WorkflowRulesService>();

builder.Services.AddScoped<IActionHandler, CreateTaskHandler>();
builder.Services.AddScoped<IActionHandler, CreateTasksForLeadsHandler>();
builder.Services.AddScoped<IActionHandler, CreateTasksFromStepsHandler>();
builder.Services.AddScoped<IActionHandler, UnblockDependentStepsHandler>();

builder.Services.AddScoped<TasksService>();
builder.Services.AddScoped<EventsService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<ProjectsService>();
builder.Services.AddScoped<TeamsService>();
builder.Services.AddScoped<ToolsService>();
 
builder.Services.AddScoped<IClaimsTransformation, KeycloakRoleTransformer>();

builder.Services.AddSignalR();
builder.Services.AddHttpClient();



builder.Services.AddControllers();
var keycloakUrl=builder.Configuration["Keycloak:BaseUrl"];
var realm=builder.Configuration["Keycloak:Realm"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keycloak expose ses clés publiques ici
        options.Authority = $"{keycloakUrl}/realms/{realm}";
        options.Audience = builder.Configuration["Keycloak:ClientId"];
        options.RequireHttpsMetadata = false; // dev seulement

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,  
            ValidateLifetime = true,
            RoleClaimType = "realm_access.roles"  
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
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
app.MapHub<Backend.Hubs.NotificationHub>("/hubs/notifications");

var workflowRulesService = app.Services.GetRequiredService<WorkflowRulesService>();

app.Run();