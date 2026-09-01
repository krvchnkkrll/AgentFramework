using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using Persistence.Contracts;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Storage;
using Persistence.Repositories;
using Persistence.Storage;

namespace Persistence;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<NpgsqlConnectionStringBuilder>().BindConfiguration("Postgres");

        void ConfigureNpgsql(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
        {
            var npgsqlConnectionStringBuilderOptions =
                serviceProvider.GetRequiredService<IOptions<NpgsqlConnectionStringBuilder>>();
            var npgsqlConnectionStringBuilder = npgsqlConnectionStringBuilderOptions.Value;
            var npgsqlConnectionString = npgsqlConnectionStringBuilder.ConnectionString;

            optionsBuilder
                .UseSnakeCaseNamingConvention()
                .UseNpgsql(
                    npgsqlConnectionString,
                    npgsqlBuilder =>
                    {
                        npgsqlBuilder.MigrationsAssembly(typeof(DbContext).Assembly.FullName);
                        npgsqlBuilder.MigrationsHistoryTable(HistoryRepository.DefaultTableName);
                    })
                .UseProjectables();
        }

        builder.Services.AddDbContextPool<DbContext>(ConfigureNpgsql)
            .AddScoped<DbContext>()
            .AddScoped<IDbContext>(static serviceProvider => serviceProvider.GetRequiredService<DbContext>());
        
        builder.Services.AddDbContextFactory<DbContext>(ConfigureNpgsql);

        builder.Services.AddSingleton<IDbContextFactory, DbContextFactory>();

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
        builder.Services.AddScoped<IAgentRepository, AgentRepository>();
        builder.Services.AddScoped<ISkillRepository, SkillRepository>();

        // Файловое хранилище. Локальная папка — временная реализация: под несколько узлов
        // сюда нужно подставить объектное хранилище, больше нигде менять ничего не придётся.
        builder.Services.AddOptions<FileStorageOptions>().BindConfiguration("FileStorage");
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

        return builder;
    }
}
