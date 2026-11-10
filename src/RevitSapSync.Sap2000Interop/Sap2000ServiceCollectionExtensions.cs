using Microsoft.Extensions.DependencyInjection;
using RevitSapSync.Core.Services;

namespace RevitSapSync.Sap2000Interop;

public static class Sap2000ServiceCollectionExtensions
{
    public static IServiceCollection AddSap2000Reader(this IServiceCollection services)
    {
        services.AddSingleton<ISapConnectionProvider, Sap2000ConnectionProvider>();
        services.AddSingleton<ISapModelReader, Sap2000FrameReader>();
        return services;
    }
}
