using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Win32;

namespace eodh.Tests.Helpers;

/// <summary>
/// Ensures ArcGIS SDK assemblies are resolvable from the moment the test
/// assembly is loaded — before any fixture, collection, or test class runs.
/// Eliminates race conditions when xUnit runs collections in parallel.
/// </summary>
internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void EnsureResolver()
    {
        TestResolver.Install("ArcGISPro");
    }
}

/// <summary>
/// Assembly resolver for ArcGIS Pro SDK DLLs.
/// Adapted from Esri's ProGuide Regression Testing sample.
/// Reads InstallDependencies.json to map assembly names to their install folders,
/// then falls back to the Pro bin directory.
/// </summary>
internal static class TestResolver
{
    private static readonly Dictionary<string, string> AssemblyMap = new();
    private static string _productId = "ArcGISPro";
    private static string _installDir = string.Empty;
    private static bool _installed;

    public static void Install(string productId = "ArcGISPro")
    {
        if (_installed) return;
        _installed = true;
        _productId = productId;
        ConfigureResolver();
        AppDomain.CurrentDomain.AssemblyResolve += CustomResolverHandler;
    }

    private static Assembly? CustomResolverHandler(object? sender, ResolveEventArgs args)
    {
        try
        {
            var filename = args.Name.Split(',')[0];
            if (filename.Contains(".resources"))
                return null;

            var match = AssemblyMap.TryGetValue(filename, out var folder);
            var dll = filename + ".dll";

            if (match && folder != null)
                return Assembly.LoadFrom(Path.Combine(folder, dll));

            var proBinPath = Path.Combine(GetProInstallLocation(), dll);
            if (File.Exists(proBinPath))
                return Assembly.LoadFrom(proBinPath);
        }
        catch
        {
            // Swallow — let the default resolver handle it
        }

        return null;
    }

    private static string GetProInstallLocation()
    {
        if (!string.IsNullOrEmpty(_installDir))
            return _installDir;

        try
        {
            var sk = Registry.LocalMachine.OpenSubKey(@$"Software\ESRI\{_productId}");
            _installDir = sk?.GetValue("InstallDir") as string ?? "";
        }
        catch
        {
            try
            {
                var sku = Registry.CurrentUser.OpenSubKey(@$"Software\ESRI\{_productId}");
                _installDir = sku?.GetValue("InstallDir") as string ?? "";
            }
            catch
            {
            }
        }

        _installDir = Path.Combine(_installDir, "bin");
        return _installDir;
    }

    private static void ConfigureResolver()
    {
        if (AssemblyMap.Count != 0)
            return;

        var installPath = GetProInstallLocation();
        var jsonPath = Path.Combine(installPath, "InstallDependencies.json");
        if (!File.Exists(jsonPath))
            return;

        try
        {
            var fileContent = File.ReadAllText(jsonPath);
            var jd = JsonDocument.Parse(fileContent);
            var root = jd.RootElement;
            var installationNode = root.GetProperty("Installation");
            var folders = installationNode.GetProperty("Folders");

            for (var i = 0; i < folders.GetArrayLength(); i++)
            {
                var folder = folders[i];
                var folderPath = folder.GetProperty("Path").ToString();
                var fullPath = Path.Combine(installPath, folderPath);
                var assemblies = folder.GetProperty("Assemblies");

                for (var j = 0; j < assemblies.GetArrayLength(); j++)
                {
                    var asmName = assemblies[j].GetProperty("Name").ToString();
                    AssemblyMap.TryAdd(asmName, fullPath);
                }
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Trace.WriteLine($"Error processing dependency file: {e.Message}");
        }
    }
}
