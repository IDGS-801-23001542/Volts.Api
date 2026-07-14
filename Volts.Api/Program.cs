using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Volts.Api.Services;
using Volts.Api.Settings;

var builder =
    WebApplication.CreateBuilder(args);

// =========================================================
// CONFIGURACIONES
// =========================================================

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

// =========================================================
// SERVICIOS DE BASE DE DATOS
// =========================================================

builder.Services.AddSingleton<MongoDbService>();

// =========================================================
// SERVICIOS DE AUTENTICACIÓN
// =========================================================

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuthService>();

// =========================================================
// SERVICIOS DE INVENTARIO Y PRODUCCIÓN
// =========================================================

builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ProductionInventorySeedService>();

// =========================================================
// SERVICIOS DE RESET Y SEED
// =========================================================

builder.Services.AddScoped<
    DevelopmentDataResetService
>();

builder.Services.AddScoped<SeedService>();

builder.Services.AddScoped<
    ProductionInventorySeedService
>();

// =========================================================
// CONTROLADORES Y SWAGGER
// =========================================================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// =========================================================
// CONFIGURACIÓN JWT
// =========================================================

var jwtSettings =
    builder.Configuration
        .GetSection("JwtSettings")
        .Get<JwtSettings>()
    ??
    throw new InvalidOperationException(
        "No existe la configuración JwtSettings."
    );

if (string.IsNullOrWhiteSpace(
        jwtSettings.SecretKey))
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey es obligatoria."
    );
}

var jwtKeyBytes =
    Encoding.UTF8.GetBytes(
        jwtSettings.SecretKey
    );

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey debe tener al menos 32 bytes para utilizar HS256."
    );
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults
                .AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults
                .AuthenticationScheme;
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

                /*
                 * Evita aceptar tokens expirados durante
                 * minutos adicionales.
                 */
                ClockSkew =
                    TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();

// =========================================================
// CORS
// =========================================================

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

// =========================================================
// CONSTRUCCIÓN DE LA APLICACIÓN
// =========================================================

var app = builder.Build();

// =========================================================
// RESET Y SEEDS
// =========================================================

using (var scope =
       app.Services.CreateScope())
{
    /*
     * 1. Reset controlado.
     *
     * Solamente elimina datos cuando:
     *
     * SeedSettings:ResetOnStart = true
     *
     * y el ambiente es Development.
     */
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
        /*
         * 2. Seed base:
         *
         * - Roles
         * - Empleado
         * - Unidades
         * - Categorías
         * - Productos
         * - Proveedores
         * - Materia prima
         * - Compras
         * - Movimientos iniciales
         */
        var seedService =
            scope.ServiceProvider
                .GetRequiredService<
                    SeedService
                >();

        await seedService.SeedAsync();

        /*
         * 3. Seed específico de Producción:
         *
         * - Recetas BOM
         * - Datos base necesarios para producción
         *
         * Se ejecuta después del SeedService porque
         * depende de productos y materias primas.
         */
        var productionInventorySeed =
            scope.ServiceProvider
                .GetRequiredService<
                    ProductionInventorySeedService
                >();

        await productionInventorySeed
            .SeedAsync();
    }
}

// =========================================================
// PIPELINE HTTP
// =========================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("VoltsCors");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();