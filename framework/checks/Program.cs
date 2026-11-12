using System;
using ProbablyStolenModTemplate;

var entry = new ModEntry();
entry.Init(new ModManifest { ID = "example", ModVersion = "0.1.0" });
entry.OnEnable();
entry.OnEnable();
ModHook.Raise("other");
Expect(ModLog.Messages.Count == 1, "other mod was ignored");
ModHook.Raise("example");
ModHook.Raise("example");
Expect(ModLog.Messages.Count == 2, "assets activated exactly once");
entry.OnDisable();
Expect(ModEntry.Installed == "", "feature resources were released");
entry.OnDisable();
ModHook.Raise("example");
Expect(ModLog.Messages.Count == 2 && HarmonyLib.Harmony.Unpatches == 1, "disable released owned hooks");
entry.OnEnable();
ModHook.Raise("example");
Expect(ModLog.Messages.Count == 4, "second enable works");
entry.OnDisable();

var broken = new ModEntry();
broken.Init(new ModManifest { ID = "broken", ModVersion = "0.1.0" });
broken.OnEnable();
ModEntry.FailAssets = true;
ModHook.Raise("broken");
ModEntry.FailAssets = false;
Expect(ModEntry.Installed == "" && broken.FormatDiagnostics().Contains("runtime/assets/failed"),
    "failed asset activation rolls back core and asset resources");
broken.OnEnable();
ModHook.Raise("broken");
Expect(ModEntry.Installed == "core assets ", "recovered after successful rollback");
broken.OnDisable();

var order = "";
var lifetime = new RuntimeLifetime(null);
try
{
    lifetime.Start(() =>
    {
        lifetime.Own(() => order += "a", () => order += "A");
        lifetime.Own(() => throw new InvalidOperationException("acquire"), () => order += "B");
    });
    throw new Exception("startup unexpectedly succeeded");
}
catch (InvalidOperationException error) when (error.Message == "acquire") { }
Expect(order == "aBA", "partial startup rolls back in reverse order");

var diagnostics = new MaintenanceDiagnostics();
var signal = new ExtensionEvent("loaded", diagnostics.Record);
using var failing = signal.Subscribe("failing", () => throw new InvalidOperationException("private detail"));
using var healthy = signal.Subscribe("healthy", () => order += "C");
signal.Publish();
signal.Publish();
string report = diagnostics.Format();
Expect(order == "aBACC" && report.Contains("failing/event.loaded/listener_failed") &&
    report.Contains("x2") && !report.Contains("private detail"), "listener isolation and safe aggregation");
Console.WriteLine("Framework checks passed.");

static void Expect(bool condition, string name)
{
    if (!condition) throw new Exception(name);
}

namespace ProbablyStolenModTemplate
{
    public sealed partial class ModEntry
    {
        internal static string Installed = "";
        internal static bool FailAssets;

        partial void InstallCore(RuntimeLifetime lifetime, HarmonyLib.Harmony harmony)
        {
            lifetime.Own(() => Installed += "core ", () => Installed = Installed.Replace("core ", ""));
        }

        partial void InstallAssets(RuntimeLifetime lifetime, LoadedMod mod)
        {
            lifetime.Own(() =>
            {
                Installed += "assets ";
                if (FailAssets) throw new InvalidOperationException("asset setup failed");
            }, () => Installed = Installed.Replace("assets ", ""));
        }
    }
}
