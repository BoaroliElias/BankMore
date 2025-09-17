using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using Transferencia.Api.Http;
using Npgsql;
using System.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Conexão Postgres via Dapper
builder.Services.AddScoped<IDbConnection>(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("Postgres")));

// MediatR — aponta para o assembly onde estão os handlers (Application)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Transferencia.Application.Internal.AssemblyMarker).Assembly));

// FluentValidation (se seus handlers recebem IValidator<> por DI)
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(typeof(Transferencia.Application.Internal.AssemblyMarker).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Transferencia.Api", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe: Bearer {seu_token_jwt}",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});

// JWT
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"] ?? jwtSection["Key"];          // mesmo valor da ContaCorrente.Api
var jwtIssuer = jwtSection["Issuer"];          // se usar
var jwtAudience = jwtSection["Audience"];        // se usar

if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException("JWT secret ausente: defina o jwt:Secret ou jwt:key no appSettings");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
#if DEBUG
        options.RequireHttpsMetadata = false;
#endif
        options.MapInboundClaims = false;

        // toggles de issuer/audience (liga só se houver valor no appsettings)
        var validateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer);
        var validateAudience = !string.IsNullOrWhiteSpace(jwtAudience);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),

            ValidateIssuer = validateIssuer,
            ValidIssuer = jwtIssuer,
            ValidateAudience = validateAudience,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };

        // força 403 para token inválido/expirado
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                ctx.NoResult();
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


// ===== HttpClient p/ ContaCorrente (forçar HTTP/1.1) =====
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardAuthHeaderHandler>(); // seu DelegatingHandler que repassa o Authorization
builder.Services.AddHttpClient("contacorrente", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["ServiceUrls:ContaCorrenteApi"]!);
    c.DefaultRequestVersion = HttpVersion.Version11;
    c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
}).AddHttpMessageHandler<ForwardAuthHeaderHandler>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();


// ===== Swagger sempre disponível =====
app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((doc, req) =>
    {
        doc.Servers = new List<OpenApiServer> { new() { Url = $"{req.Scheme}://{req.Host.Value}" } };
    });
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transferencia.Api v1");
    c.RoutePrefix = "swagger";
});

var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
if (!inContainer) app.UseHttpsRedirection();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
