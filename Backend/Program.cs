using Backend.Data;
using Backend.Kafka;
using Backend.Modules.Events.Services;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Tasks.Services;

var builder = WebApplication.CreateBuilder(args);

// Base de données
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration
        .GetConnectionString("DefaultConnection")));

// Services Kafka
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddSingleton<KafkaProducerService>();

// Services métier
builder.Services.AddScoped<EventProcessorService>();

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<TasksService>();

// CORS pour Angular
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

// Créer la base si elle n'existe pas
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

app.Run();