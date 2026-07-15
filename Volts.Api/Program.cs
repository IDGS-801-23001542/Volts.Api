using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Volts.Api.Filters;
using Volts.Api.Middleware;
using Volts.Api.Services;
using Volts.Api.Settings;

var builder =
    WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(
        "MongoDbSettings"
    )
);

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(
        "JwtSettings"
    )
);

builder.Services.Configure<SeedSettings>(
    builder.Configuration.GetSection(
        "SeedSettings"
    )
);

builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditTrailService>();
builder.Services.AddScoped<NotificationDispatchService>();
builder.Services.AddScoped<PermissionCatalogService>();
builder.Services.AddScoped<TemporaryPasswordService>();

builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<
    ProductionInventorySeedService
>();
builder.Services.AddScoped<
    DevelopmentDataResetService
>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<
    CommercialDemoSeedService
>();

builder.Services.AddScoped<
    SensitiveDataSanitizer
>();
builder.Services.AddScoped<
    AuditActionFilter
>();

builder.Services.AddControllers(options =>
{
    /*
     * Captura automáticamente POST, PUT, PATCH y DELETE
     * exitosos sin modificar cada controlador.
     */
    options.Filters.AddService<
        AuditActionFilter
    >();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtSettings =
    builder.Configuration
        .GetSection("JwtSettings")
        .Get<JwtSettings>()
    ??
    throw new InvalidOperationException(
        "No existe la configuración JwtSettings."
    );

var jwtKeyBytes =
    Encoding.UTF8.GetBytes(
        jwtSettings.SecretKey
    );

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey debe tener al menos 32 bytes."
    );
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer =
                    jwtSettings.Issuer,
                ValidAudience =
                    jwtSettings.Audience,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        jwtKeyBytes
                    ),
                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "VoltsCors",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    );
});

var app = builder.Build();

using (var scope =
       app.Services.CreateScope())
{
    var resetService =
        scope.ServiceProvider
            .GetRequiredService<
                DevelopmentDataResetService
            >();

    await resetService.ResetAsync();

    var seedSettings =
        scope.ServiceProvider
            .GetRequiredService<
                IOptions<SeedSettings>
            >()
            .Value;

    if (seedSettings.SeedOnStart)
    {
        await scope.ServiceProvider
            .GetRequiredService<SeedService>()
            .SeedAsync();

        await scope.ServiceProvider
            .GetRequiredService<
                ProductionInventorySeedService
            >()
            .SeedAsync();

        await scope.ServiceProvider
            .GetRequiredService<
                CommercialDemoSeedService
            >()
            .SeedAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

/*
 * Debe colocarse antes de autenticación y controladores.
 */
app.UseMiddleware<
    ExceptionLoggingMiddleware
>();

app.UseHttpsRedirection();
app.UseCors("VoltsCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
