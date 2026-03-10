using Backend.Data;
using Backend.Kafka;
using Backend.Modules.Events.Services;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Tasks.Services;

var builder = WebApplication.CreateBuilder(args);
 
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration
        .GetConnectionString("DefaultConnection")));

 
builder.Services.AddHostedService<KafkaConsumerService>();
//builder.Services.AddSingleton<KafkaProducerService>();

builder.Services.AddHostedService<OutboxPublisherService>();
 
builder.Services.AddScoped<EventProcessorService>();
builder.Services.AddScoped<TasksService>();


builder.Services.AddControllers();

 



var app = builder.Build();

 
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

 
 

 
app.UseAuthorization();
app.MapControllers();

app.Run();