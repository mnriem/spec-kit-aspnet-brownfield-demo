using Carrotware.CMS.Data.Models;
using Carrotware.CMS.HeadlessApi.Services;
using Carrotware.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var config = builder.Configuration;

// Logging — CarrotFileLogger wired from appsettings Logging:CarrotLogger section
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
#if DEBUG
builder.Logging.AddDebug();
#endif

services.AddSingleton<IConfigurationRoot>(config as IConfigurationRoot
	?? throw new InvalidOperationException("Configuration is not an IConfigurationRoot."));

services.AddCarrotFileLogger();

// Database
services.AddDbContext<CarrotCakeContext>(opt =>
	opt.UseSqlServer(config.GetConnectionString(CarrotCakeContext.DBKey)));

// Infrastructure
services.AddMemoryCache();
services.AddResponseCaching();
services.AddProblemDetails();
services.AddHealthChecks();
services.AddHttpContextAccessor();

// Application services
services.AddScoped<IContentQueryService, ContentQueryService>();
services.AddScoped<ITokenService, TokenService>();

// JWT Bearer authentication
// Key sourced from CARROT_HEADLESS_JWT_KEY env var with HeadlessApi:JwtKey config fallback
var rawJwtKey = Environment.GetEnvironmentVariable("CARROT_HEADLESS_JWT_KEY")
	?? config["HeadlessApi:JwtKey"]
	?? throw new InvalidOperationException("HeadlessApi JWT key is not configured. Set CARROT_HEADLESS_JWT_KEY env var or HeadlessApi:JwtKey in appsettings.");

byte[] jwtKeyBytes;
try {
	jwtKeyBytes = Convert.FromBase64String(rawJwtKey);
} catch (FormatException) {
	// Allow plain-string key in development; use UTF8 bytes
	jwtKeyBytes = Encoding.UTF8.GetBytes(rawJwtKey);
}

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options => {
		options.TokenValidationParameters = new TokenValidationParameters {
			ValidateIssuer = true,
			ValidIssuer = config["HeadlessApi:Issuer"],
			ValidateAudience = true,
			ValidAudience = config["HeadlessApi:Audience"],
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromSeconds(30),
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
		};
	});

services.AddAuthorization();
services.AddControllers();

var app = builder.Build();

app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
