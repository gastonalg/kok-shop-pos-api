using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger con botón Authorize (Bearer)
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "pos", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Ingresá: Bearer {tu_token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] {} }
    });
});

// CORS (para que puedas pegarle desde front / swagger sin dramas)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()
    );
});

// JWT Auth
var secret = builder.Configuration["JWT_SECRET"];

// Ojo: si no existe, igual levanta, pero login te lo va a decir (500 con mensaje claro).
// Para endpoints protegidos, si secret falta, no conviene romper el arranque en producción.
// Si querés que falle al iniciar si falta, te lo cambio.
if (!string.IsNullOrWhiteSpace(secret))
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;
            options.SaveToken = true;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,

                ValidateIssuer = false,
                ValidateAudience = false,

                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
}
else
{
    // Igual registramos auth para que la app no explote.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();
}

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

// Si estás detrás de Railway reverse-proxy
app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok("OK"));
app.MapControllers();

app.Run();
