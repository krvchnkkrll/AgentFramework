using Application.Contracts.Services;
using Application.Services;
using Assistant.Contracts.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Application;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddApplication(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        builder.Services.AddSingleton<IGenerationRegistryService, GenerationRegistryService>();

        // Мост между ассистентом и файловым хранилищем: ассистенту нужен архив скилла,
        // а где он лежит — знает приложение.
        builder.Services.AddSingleton<ISkillPackageSource, SkillPackageSource>();
        
        return builder;
    }
}
