using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Persistence.Contracts;
using Persistence.Contracts.Repositories;
using Persistence.Repositories;

namespace Persistence;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<NpgsqlConnectionStringBuilder>().BindConfiguration("Postgres");

        builder.Services.AddDbContextPool<DbContext>(static (serviceProvider, optionsBuilder) =>
            {
                var npgsqlConnectionStringBuilderOptions =
                    serviceProvider.GetRequiredService<IOptions<NpgsqlConnectionStringBuilder>>();
                var npgsqlConnectionStringBuilder = npgsqlConnectionStringBuilderOptions.Value;
                var npgsqlConnectionString = npgsqlConnectionStringBuilder.ConnectionString;

                optionsBuilder
                    .UseSnakeCaseNamingConvention()
                    .UseNpgsql(
                        npgsqlConnectionString,
                        builder =>
                        {
                            builder.MigrationsAssembly(typeof(DbContext).Assembly.FullName);
                            builder.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
                        })
                    .UseProjectables();
            })
            .AddScoped<DbContext>()
            .AddScoped<IDbContext>(static serviceProvider => serviceProvider.GetRequiredService<DbContext>());

        builder.Services.AddScoped<IUserRepository, UserRepository>();

        return builder;
    }
}
