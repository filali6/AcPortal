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
using Backend.Modules.Notifications.Services;
using Backend.Modules.Chat.Services;
using Backend.Modules.AI.Configurators;
using Backend.Modules.Dashboard.Services;
using Backend.Modules.Git.Services;
using Backend.Modules.Planning.Services;
using Backend.Modules.Planning.Tools;
using Backend.Modules.Sla.Jobs;
using Backend.Modules.AI.Services;
using Backend.Modules.Sla.Tools;

using Prometheus;


using Backend.Modules.Messaging.Services;
using Backend.Modules.Sla.Services;
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
builder.Services.AddScoped<PluginRegistry>();

builder.Services.AddScoped<EventPublisher>();

builder.Services.AddScoped<IClaimsTransformation, KeycloakRoleTransformer>();
builder.Services.AddScoped<ContractsService>();

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
builder.Services.AddScoped<IContractSummaryService, ContractSummaryService>();
builder.Services.AddScoped<IActionHandler, SummarizeContractHandler>();
builder.Services.AddScoped<IContractSummaryService, ContractSummaryService>();
builder.Services.AddScoped<BriefingService>();

builder.Services.AddScoped<TaskCommentsService>();
builder.Services.AddHttpClient<TeamsNotificationService>();
builder.Services.AddScoped<TeamsNotificationService>();

builder.Services.AddScoped<IGitProvider, GitHubProvider>();
builder.Services.AddScoped<GitService>();

//builder.Services.AddScoped<GraphService>();
builder.Services.AddScoped<IMessagingProvider, SlackMessagingProvider>();
builder.Services.AddScoped<IActionHandler, CreateMessagingChannelHandler>();
builder.Services.AddScoped<MessagingCommentSyncService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IMessagingProvider, SlackMessagingProvider>();
builder.Services.AddScoped<IActionHandler, SendCommentEmailHandler>();

builder.Services.AddScoped<PlanningTools>();
builder.Services.AddScoped<FsdPlanningService>();

builder.Services.AddScoped<SlaCheckerService>();
builder.Services.AddScoped<SlaAgentService>();
builder.Services.AddHostedService<SlaWeeklyReportJob>();
builder.Services.AddScoped<KernelInvocationHelper>();
builder.Services.AddScoped<SlaAgentTools>();


builder.Services.AddMemoryCache();

var kernelBuilder = builder.Services.AddKernel();
var provider = builder.Configuration["AI:Provider"]!;

var configurators = new List<IKernelConfigurator> { new GeminiKernelConfigurator(), new GroqKernelConfigurator() };
var factory = new KernelConfiguratorFactory(configurators);
factory.Get(provider).Configure(kernelBuilder, builder.Configuration);

builder.Services.AddSignalR();
builder.Services.AddHttpClient();

builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var keycloakUrl = builder.Configuration["Keycloak:BaseUrl"];
// PublicBaseUrl lets the backend reach Keycloak over the internal docker network
// while still validating the issuer claim of tokens minted for the browser (localhost).
var keycloakPublicUrl = builder.Configuration["Keycloak:PublicBaseUrl"] ?? keycloakUrl;
var realm = builder.Configuration["Keycloak:Realm"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = $"{keycloakUrl}/realms/{realm}/.well-known/openid-configuration";
        options.Audience = builder.Configuration["Keycloak:ClientId"];
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{keycloakPublicUrl}/realms/{realm}",
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
builder.Services.AddDaprPubSubClient((_, clientBuilder) =>
{
    clientBuilder.UseGrpcEndpoint("http://localhost:50002");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseCors("AllowAngular");

app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Backend.Hubs.NotificationHub>("/hubs/notifications");

app.MapHub<Backend.Hubs.ChatHub>("/hubs/chat");
app.MapMetrics();

var workflowRulesService = app.Services.GetRequiredService<WorkflowRulesService>();

await app.RunAsync();