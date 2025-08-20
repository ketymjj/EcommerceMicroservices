using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // 👈 necessário para Swagger Security
using Shared.Messaging.Interfaces;
using Shared.Messaging.Clients;
using Shared.Security.Services;
using Shared.Security.Interfaces;
using System.Text;
using Shared.Data;
using Microsoft.AspNetCore.Identity;
using Shared.Models.AuthUser;

var builder = WebApplication.CreateBuilder(args);

// Configurações
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddScoped<IPasswordHasher<UserModel>, PasswordHasher<UserModel>>();

// RabbitMQ
var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");
builder.Services.AddSingleton<IRabbitMqClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RabbitMqClient>>();
    return new RabbitMqClient(
        hostName: rabbitConfig["HostName"]!,
        userName: rabbitConfig["UserName"]!,
        password: rabbitConfig["Password"]!,
        port: int.Parse(rabbitConfig["Port"]!),
        virtualHost: rabbitConfig["VirtualHost"]!,
        logger: logger
    );
});

// JWT
builder.Services.AddSingleton<IJwtTokenService>(sp =>
{
    var key = builder.Configuration["Jwt:Key"]!;
    var issuer = builder.Configuration["Jwt:Issuer"]!;
    var expiry = int.Parse(builder.Configuration["Jwt:ExpiryMinutes"]!);
    return new JwtTokenGenerator(key, issuer, expiry);
});

var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtExpiry = builder.Configuration["Jwt:ExpiryMinutes"];

if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtExpiry))
{
    throw new Exception("Configuração JWT ausente. Verifique appsettings.json");
}

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 🔐 Configuração Swagger com suporte a JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StockService API",
        Version = "v1"
    });

    // Define o esquema de segurança JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Digite 'Bearer {seu token JWT}'"
    });

    // Aplica a segurança para todos os endpoints
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
