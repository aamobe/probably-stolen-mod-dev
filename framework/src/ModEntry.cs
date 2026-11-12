using System;
using HarmonyLib;

namespace ProbablyStolenModTemplate;

public sealed partial class ModEntry : IMod
{
    private ModManifest _manifest;
    private ModLog _log;
    private Harmony _harmony;
    private RuntimeLifetime _lifetime;
    private readonly MaintenanceDiagnostics _diagnostics = new();
    private string _phase = "disabled";

    public void Init(ModManifest manifest)
    {
        _manifest = manifest;
        _log = new ModLog(manifest);
        _harmony = new Harmony("mod." + manifest.ID);
        _lifetime = new RuntimeLifetime(error => _diagnostics.Record("runtime", "release", "failed", error));
    }

    public void OnEnable()
    {
        try
        {
            if (!_lifetime.Start(() =>
            {
                _diagnostics.Clear();
                _phase = "waiting_for_assets";
                _lifetime.Own(() => ModHook.OnModAssetsLoaded += OnAssetsLoaded,
                    () => ModHook.OnModAssetsLoaded -= OnAssetsLoaded);
                _lifetime.Own(() => InstallCore(_lifetime, _harmony),
                    () => _harmony.UnpatchAll(_harmony.Id));
            })) return;
            _log.Log($"{_manifest.ID} {_manifest.ModVersion} 已启用");
        }
        catch (Exception error)
        {
            ReportStartupFailure("enable", error);
            throw;
        }
    }

    public void OnDisable()
    {
        _lifetime.Stop();
        _phase = _lifetime.IsFaulted ? "cleanup_failed" : "disabled";
        if (_lifetime.IsFaulted) _log.Error(FormatDiagnostics());
    }

    private void OnAssetsLoaded(LoadedMod mod)
    {
        if (_phase != "waiting_for_assets" || mod.Manifest.ID != _manifest.ID) return;
        _phase = "activating_assets";
        try
        {
            _lifetime.Continue(() => InstallAssets(_lifetime, mod));
            _phase = "ready";
            _log.Log($"{_manifest.ID} {_manifest.ModVersion} 资源已就绪");
        }
        catch (Exception error)
        {
            ReportStartupFailure("assets", error);
        }
    }

    private void ReportStartupFailure(string stage, Exception error)
    {
        _lifetime.Stop();
        _phase = _lifetime.IsFaulted ? "cleanup_failed" : "startup_failed";
        _diagnostics.Record("runtime", stage, "failed", error);
        _log.Error(FormatDiagnostics());
    }

    internal string FormatDiagnostics() =>
        $"[{_manifest.ID} {_manifest.ModVersion}] runtime={_phase}\n{_diagnostics.Format()}";

    internal void WriteDiagnostics() => _log.Log(FormatDiagnostics());

    partial void InstallCore(RuntimeLifetime lifetime, Harmony harmony);
    partial void InstallAssets(RuntimeLifetime lifetime, LoadedMod mod);
}
