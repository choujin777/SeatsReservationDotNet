using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SeatsReservationDotNet.Data;
using SeatsReservationDotNet.Services;

var builder = WebApplication.CreateBuilder(args);

// Database — prefer the configured connection string, but allow env vars to override it.
var dbConfig = builder.Configuration.GetSection("Database");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    var csBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = dbConfig["Host"] ?? "localhost",
        Port = int.Parse(dbConfig["Port"] ?? "5432"),
        Database = dbConfig["Name"] ?? "seats_reservation",
        Username = Environment.GetEnvironmentVariable("DB_USERNAME") ?? dbConfig["Username"] ?? "postgres",
        Password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? dbConfig["Password"],
        SearchPath = "base_schema"
    };

    connectionString = csBuilder.ConnectionString;
}
else
{
    var csBuilder = new NpgsqlConnectionStringBuilder(connectionString)
    {
        SearchPath = "base_schema"
    };

    var username = Environment.GetEnvironmentVariable("DB_USERNAME");
    if (!string.IsNullOrWhiteSpace(username))
    {
        csBuilder.Username = username;
    }

    var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
    if (!string.IsNullOrWhiteSpace(password))
    {
        csBuilder.Password = password;
    }

    connectionString = csBuilder.ConnectionString;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddScoped<ISessionService, SessionService>();

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Seats Reservation API", Version = "v1" });
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, message) = error switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, error.Message),
            _ => (StatusCodes.Status500InternalServerError, "An internal error occurred")
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = message });
    });
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
