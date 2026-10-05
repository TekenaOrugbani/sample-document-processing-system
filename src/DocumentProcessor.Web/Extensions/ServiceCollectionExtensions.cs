using Amazon;
using Amazon.BedrockRuntime;
using DocumentProcessor.Web.Configuration;
using DocumentProcessor.Web.Data;
using DocumentProcessor.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocumentProcessor.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BedrockOptions>()
            .Bind(configuration.GetSection(BedrockOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddDocumentProcessing(
        this IServiceCollection services,
        DatabaseConnection connection)
    {
        // A factory rather than a scoped DbContext: Blazor Server circuits outlive a
        // request, and a shared context cannot serve overlapping renders.
        services.AddDbContextFactory<AppDbContext>(options => _ = connection.Info.Provider switch
        {
            DatabaseProvider.PostgreSql => options.UseNpgsql(connection.ConnectionString),
            DatabaseProvider.SqlServer => throw new NotSupportedException("SQL Server is no longer supported. Migrate to PostgreSQL."),
            _ => throw new NotSupportedException(
                $"Database provider '{connection.Info.Provider}' is not supported.")
        });
        services.AddSingleton(connection.Info);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAmazonBedrockRuntime>(provider =>
        {
            var region = provider.GetRequiredService<IOptions<BedrockOptions>>().Value.Region;
            return new AmazonBedrockRuntimeClient(new AmazonBedrockRuntimeConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            });
        });

        services.AddSingleton<IDocumentStorage, FileSystemDocumentStorage>();
        services.AddSingleton<DocumentTextExtractor>();
        services.AddSingleton<IDocumentSummarizer, BedrockDocumentSummarizer>();
        services.AddScoped<DocumentPipeline>();

        return services;
    }
}
