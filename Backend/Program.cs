using Backend.Data;
using Backend.Modules.Auth.Services;
using Backend.Modules.Events.Services;
using Backend.Modules.Projects.Services;
using Backend.Modules.Tools.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Backend.Modules.Tasks.Services;
using Microsoft.IdentityModel.Tokens;
using Backend.Modules.Events.Handlers;
using Microsoft.AspNetCore.Authentication;
using Backend.Modules.Auth;
using System.Security.Claims;
using Dapr.Messaging.PublishSubscribe.Extensions;  
using Backend.Modules.Contracts.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration
        .GetConnectionString("DefaultConnection")),
    ServiceLifetime.Transient);

builder.Services.AddDaprClient();

builder.Services.AddDaprPubSubClient();
builder.Services.AddSingleton<StreamingSubscriptionService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<StreamingSubscriptionService>());

builder.Services.AddScoped<EventProcessorService>();
builder.Services.AddSingleton<WorkflowRulesService>();

builder.Services.AddScoped<IActionHandler, CreateTaskHandler>();
 builder.Services.AddScoped<IActionHandler, CreateTasksFromStepsHandler>();
 
builder.Services.AddScoped<TasksService>();
builder.Services.AddScoped<EventsService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<ProjectsService>();
 
builder.Services.AddScoped<ToolsService>();
builder.Services.AddSingleton<PluginRegistry>();

builder.Services.AddScoped<EventPublisher>();

builder.Services.AddScoped<IClaimsTransformation, KeycloakRoleTransformer>();
builder.Services.AddScoped<ContractsService>();

builder.Services.AddSignalR();
builder.Services.AddHttpClient();

builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles; // ✅
});

var keycloakUrl = builder.Configuration["Keycloak:BaseUrl"];
var realm = builder.Configuration["Keycloak:Realm"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{keycloakUrl}/realms/{realm}";
        options.Audience = builder.Configuration["Keycloak:ClientId"];
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role
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

builder.Services.AddDaprClient(builder =>
{
    builder.UseHttpEndpoint("http://localhost:3500");
});
builder.Services.AddDaprPubSubClient((_, clientBuilder) =>
{
    clientBuilder.UseGrpcEndpoint("http://localhost:50002");
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
// ❌ SUPPRIMÉ : app.UseCloudEvents();
// ❌ SUPPRIMÉ : app.MapSubscribeHandler();
app.MapControllers();
app.MapHub<Backend.Hubs.NotificationHub>("/hubs/notifications");

var workflowRulesService = app.Services.GetRequiredService<WorkflowRulesService>();

app.Run();