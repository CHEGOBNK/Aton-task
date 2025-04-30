using Aton_task.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(
        "Basic", options => { });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddExceptionHandler(options =>
{
    options.ExceptionHandler = async (context) =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred" });
    };
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    {
        Title = "Users API", 
        Version = "v1",
        Description = "API for managing users with CRUD operations"
    });

    c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Basic",
        In = ParameterLocation.Header,
        Description = "Welcome in Aton"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Basic"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Users API v1"));

var cache = app.Services.GetRequiredService<IMemoryCache>();
const string CACHE_KEY = "UsersCache";

if (!cache.TryGetValue(CACHE_KEY, out List<User>? initial))
{
    cache.Set(CACHE_KEY, new List<User> {
        new User
        {
            Guid = Guid.NewGuid(),
            Login = "Admin",
            Password = "Admin123",
            Name = "Админ",
            Gender = 2,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "System",
            Admin = true
        }
    });
}

app.MapControllers();
app.Run();