using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RevitSapSync.Addin;

/// <summary>
/// Revit's process can already have older versions of common libraries
/// (<c>Microsoft.Extensions.*</c>, <c>CommunityToolkit.Mvvm</c>) loaded by the time this
/// add-in loads - confirmed live via Add-in Manager, which threw a
/// <see cref="TypeLoadException"/> trying to resolve <c>ServiceCollection</c> against a
/// pre-loaded v2.2.0.0 <c>Microsoft.Extensions.DependencyInjection.Abstractions</c> when
/// this project is built against v8.0.x. .NET Framework doesn't isolate assembly loading
/// per add-in the way .NET Core's <c>AssemblyLoadContext</c> does, so whichever version
/// loaded first can "win" for the whole process unless something intervenes.
/// <para>
/// The <see cref="ModuleInitializerAttribute"/> guarantees <see cref="Initialize"/> runs
/// before any other code in this module executes - including before
/// <c>RevitSapSyncApplication.OnStartup</c> or any command's <c>Execute</c> - so the
/// resolve handler is registered before anything here can trigger the conflicting load.
/// </para>
/// </summary>
internal static class AssemblyResolver
{
    private static readonly string[] ManagedPrefixes =
    {
        "Microsoft.Extensions.",
        "CommunityToolkit.Mvvm",
    };

    [ModuleInitializer]
    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAddinDirectory;
    }

    private static Assembly? ResolveFromAddinDirectory(object? sender, ResolveEventArgs args)
    {
        string requestedName = new AssemblyName(args.Name).Name ?? string.Empty;
        if (!ManagedPrefixes.Any(prefix => requestedName.StartsWith(prefix, StringComparison.Ordinal)))
            return null;

        string? directory = Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location);
        if (directory is null)
            return null;

        string candidatePath = Path.Combine(directory, requestedName + ".dll");
        return File.Exists(candidatePath) ? Assembly.LoadFrom(candidatePath) : null;
    }
}
