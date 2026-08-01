using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Persistence.Contracts.Services;
using Sso.Options;
using Sso.Services;

namespace Sso;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddSso(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<KeycloakOptions>().BindConfiguration("Keycloak").ValidateOnStart();
        
        var keycloakOptions = builder.Configuration.GetSection("Keycloak").Get<KeycloakOptions>()
            ?? throw new InvalidOperationException("Configuration section 'Keycloak' is missing.");

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddTransient<IClaimsTransformation, KeycloakRoleClaimsTransformation>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority;
                options.Audience = keycloakOptions.Audience;

                // Local/dev Keycloak often runs plain HTTP; only demand HTTPS metadata
                // when the configured authority actually is HTTPS (i.e. in real environments).
                options.RequireHttpsMetadata =
                    keycloakOptions.Authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloakOptions.Authority,
                    ValidateAudience = true,
                    ValidAudience = keycloakOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                };

                // WebSocket connections can't set an Authorization header, so the
                // SignalR JS client sends the token via query string instead — accept
                // it there, but only for the hub path (never for regular API routes).
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}
