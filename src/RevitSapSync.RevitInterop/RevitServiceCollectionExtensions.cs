using Microsoft.Extensions.DependencyInjection;
using RevitSapSync.Core.Services;

namespace RevitSapSync.RevitInterop;

public static class RevitServiceCollectionExtensions
{
    public static IServiceCollection AddRevitFrameReading(this IServiceCollection services)
    {
        services.AddSingleton<IRevitContextAccessor, RevitContextAccessor>();
        services.AddSingleton<IRevitFrameReader, RevitFrameReader>();
        services.AddSingleton<IRevitFrameReadDispatcher, RevitApiDispatcher>();
        services.AddSingleton<IGeometrySyncEngine, GeometrySyncEngine>();
        services.AddSingleton<IGeometrySyncDispatcher, GeometrySyncDispatcher>();
        services.AddSingleton<ISectionSyncEngine, SectionSyncEngine>();
        services.AddSingleton<ISectionSyncDispatcher, SectionSyncDispatcher>();
        return services;
    }
}
