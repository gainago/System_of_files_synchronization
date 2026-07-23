using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // 👈 Добавлена эта строка
using System.Text;
using Server.Core.Interfaces;
using Server.Storage;
using Server.FileSystem;
using Server.Auth; // 👈 Добавлена эта строка
using SyncServer.Services;


var builder = WebApplication.CreateBuilder(args);

// 1. Контроллеры и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sync Server API",
        Version = "v1",
        Description = "API для синхронизации файлов"
    });

    // Добавляем поддержку JWT в Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
            Array.Empty<string>()
        }
    });
});

// 2. JWT Аутентификация
var secretKey = "YourSuperSecretKey1234567890!@#$%^&*()";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = "SyncServer",
            ValidateAudience = true,
            ValidAudience = "SyncClient",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// 3. Регистрация модулей
// 3. Регистрация модулей
var env = builder.Environment;
var syncFolderPath = Path.Combine(env.ContentRootPath, "SyncData");
var backupFolderPath = Path.Combine(env.ContentRootPath, "BackupData");
var dbPath = Path.Combine(env.ContentRootPath, "sync.db");

// Создаем папки заранее
Directory.CreateDirectory(syncFolderPath);
Directory.CreateDirectory(backupFolderPath);

builder.Services.AddSingleton<IServerDatabase>(new SqliteDatabase($"Data Source={dbPath}"));
builder.Services.AddSingleton<IServerFileStorage>(new LocalFileStorage(syncFolderPath, backupFolderPath));
builder.Services.AddSingleton<IAuthService>(new AuthService(secretKey));

// 4. Регистрация бизнес-логики
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<ITransactionService, TransactionService>();

var app = builder.Build();

// 5. Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sync Server API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();