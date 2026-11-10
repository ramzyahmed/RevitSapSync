using Microsoft.Extensions.DependencyInjection;
using RevitSapSync.App.ViewModels;
using RevitSapSync.App.Views;

namespace RevitSapSync.App.Extensions;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddRevitSapSyncApp(this IServiceCollection services)
    {
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
        return services;
    }
}
