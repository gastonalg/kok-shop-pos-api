using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "pos", Version = "v1" });

    // Bearer en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header usando el esquema Bearer."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// JWT Auth (no rompe si falta JWT_SECRET)
var jwtSecret = builder.Configuration["JWT_SECRET"];
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });
}
else
{
    // Si falta, el login igual te puede andar (porque no depende de UseAuthentication),
    // pero no vas a poder proteger endpoints hasta setear JWT_SECRET.
    builder.Logging.AddConsole();
}

var app = builder.Build();

// Railway/Reverse proxy: confía en headers (opcional, pero ayuda)
app.UseForwardedHeaders();

// Swagger siempre (así probás fácil)
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "OK");

app.UseHttpsRedirection();

// Si configuraste JWT_SECRET, esto habilita auth
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
