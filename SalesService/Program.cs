using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Shared.Messaging.Interfaces;
using Shared.Messaging.Clients;
using Shared.Data;
using Microsoft.AspNetCore.Identity;
using Shared.Models.AuthUser;
using Shared.Security.Interfaces;
using Shared.Security.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ----------------------
// Configurações do DbContext
// ----------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddScoped<IPasswordHasher<UserModel>, PasswordHasher<UserModel>>();

// ----------------------
// Configurações do RabbitMQ
// ----------------------
var rabbitConfig = builder.Configuration.GetSection("RabbitMQ");

builder.Services.AddSingleton<IRabbitMqClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RabbitMqClient>>();

    return new RabbitMqClient(
        hostName: rabbitConfig["HostName"] ?? "localhost",
        userName: rabbitConfig["UserName"] ?? "guest",
        password: rabbitConfig["Password"] ?? "guest",
        port: int.Parse(rabbitConfig["Port"] ?? "5672"),
        virtualHost: rabbitConfig["VirtualHost"] ?? "/",
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

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SalesService API", Version = "v1" });

    // 🔒 Configuração do JWT no Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT assim: Bearer {seu token}"
    });

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
            new string[] {}
        }
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Mostra erros detalhados
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SalesService API V1");
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
