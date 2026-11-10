using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RevitSapSync.Addin;

/// <summary>
/// Add-in Manager shadow-copies this entry assembly into a fresh temp folder on every
/// load (confirmed: every sibling DLL, including <c>RevitSapSync.Core.dll</c>, gets
/// copied there too, all with matching fresh timestamps) - but that alone doesn't help.
/// Every build produces the same default <c>AssemblyVersion</c> ("1.0.0.0"), so once the
/// CLR has bound the name "RevitSapSync.Core" to an in-memory copy during this Revit
/// process's lifetime, normal name-based resolution keeps reusing that copy for the rest
/// of the session regardless of what's actually on disk - the exact mechanism that made
/// every Add-in-Manager reload run stale <c>RevitInterop</c>/<c>Sap2000Interop</c>/<c>App</c>
/// code before those got merged into this project. <c>Assembly.Load(byte[])</c> sidesteps
/// this: a byte-array load always creates a new, independent assembly identity outside
/// the CLR's normal name-based binding, even for a simple name the AppDomain has already
/// seen.
/// <para>
/// <b>This alone is not enough.</b> <see cref="AppDomain.AssemblyResolve"/> only fires
/// when .NET Framework's normal probing FAILS to find a matching file - and it always
/// succeeds here, because a plain <c>RevitSapSync.Core.dll</c> sits right next to this
/// assembly in the same deployed folder. So the fresh byte-loaded copy was being built for
/// nothing: normal probing kept satisfying every request before <see cref="ResolveCoreAssembly"/>
/// ever got a chance to run. The fix is to never deploy a file literally named
/// <c>RevitSapSync.Core.dll</c> - see the renamed-copy step in
/// RevitSapSync.Addin.csproj's deploy targets - so normal probing has nothing to find and
/// <see cref="AppDomain.AssemblyResolve"/> is guaranteed to fire.
/// </para>
/// <para>
/// Registered via a module initializer so it runs before any other code in this module -
/// including <see cref="RevitSapSyncApplication.OnStartup"/> or any command's
/// <c>Execute</c> - can possibly touch a RevitSapSync.Core type first.
/// </para>
/// <para>
/// <b>One caveat that has no workaround</b>: if "RevitSapSync.Core" was already
/// normal-resolved earlier in the current Revit session (e.g. before this fix existed),
/// that binding is permanent for the life of the process - .NET Framework cannot unload or
/// replace an already-loaded assembly. This fix only takes effect starting from the next
/// fresh Revit session; from that point on, no further restarts should be needed across
/// repeated Add-in Manager reload/test cycles.
/// </para>
/// </summary>
internal static class CoreAssemblyLoader
{
    private const string CoreAssemblySimpleName = "RevitSapSync.Core";

    // Must match the renamed-copy filename produced by the deploy targets in
    // RevitSapSync.Addin.csproj - deliberately NOT "RevitSapSync.Core.dll" so that no
    // file with that exact name exists for normal assembly probing to find.
    private const string CoreAssemblyFileName = "RevitSapSync.Core.reflection.dll";
    private const string CoreSymbolsFileName = "RevitSapSync.Core.reflection.pdb";

    private static Assembly? _freshCoreAssembly;

    [ModuleInitializer]
    public static void Initialize()
    {
        string? directory = Path.GetDirectoryName(typeof(CoreAssemblyLoader).Assembly.Location);
        if (directory is null)
            return;

        string corePath = Path.Combine(directory, CoreAssemblyFileName);
        if (!File.Exists(corePath))
            return;

        byte[] assemblyBytes = File.ReadAllBytes(corePath);
        string symbolsPath = Path.Combine(directory, CoreSymbolsFileName);
        _freshCoreAssembly = File.Exists(symbolsPath)
            ? Assembly.Load(assemblyBytes, File.ReadAllBytes(symbolsPath))
            : Assembly.Load(assemblyBytes);

        AppDomain.CurrentDomain.AssemblyResolve += ResolveCoreAssembly;
    }

    private static Assembly? ResolveCoreAssembly(object? sender, ResolveEventArgs args)
    {
        string requestedName = new AssemblyName(args.Name).Name ?? string.Empty;
        return requestedName == CoreAssemblySimpleName ? _freshCoreAssembly : null;
    }
}
