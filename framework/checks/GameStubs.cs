using System;
using System.Collections.Generic;

public interface IMod
{
    void Init(ModManifest manifest);
    void OnEnable();
    void OnDisable();
}

public sealed class ModManifest
{
    public string ID;
    public string ModVersion;
}

public sealed class LoadedMod
{
    public ModManifest Manifest;
}

public static class ModHook
{
    public static event Action<LoadedMod> OnModAssetsLoaded;
    public static void Raise(string id) =>
        OnModAssetsLoaded?.Invoke(new LoadedMod { Manifest = new ModManifest { ID = id } });
}

public sealed class ModLog
{
    public static readonly List<string> Messages = new();
    public ModLog(ModManifest manifest) { }
    public void Log(string message) => Messages.Add(message);
    public void Error(string message) => Messages.Add(message);
}

namespace HarmonyLib
{
    public sealed class Harmony
    {
        public static int Unpatches;
        public string Id { get; }
        public Harmony(string id) { Id = id; }
        public void UnpatchAll(string id)
        {
            if (id != Id) throw new ArgumentException(nameof(id));
            Unpatches++;
        }
    }
}
