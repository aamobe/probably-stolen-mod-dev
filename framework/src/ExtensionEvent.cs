using System;
using System.Collections.Generic;
namespace ProbablyStolenModTemplate;

// Main-thread lifecycle notification, never a payment/result-combining API.
internal sealed class ExtensionEvent
{
    private readonly string _name;
    private readonly List<Lease> _listeners = new();
    private bool _publishing;
    private readonly Action<string, string, string, Exception> _report;
    internal ExtensionEvent(string name, Action<string, string, string, Exception> report)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Event name required", nameof(name));
        _name = name;
        _report = report;
    }
    internal IDisposable Subscribe(string owner, Action callback)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner required", nameof(owner));
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        foreach (var lease in _listeners)
            if (lease.Owner == owner) throw new InvalidOperationException("Duplicate event owner: " + owner);
        var entry = new Lease(this, owner, callback);
        _listeners.Add(entry);
        return entry;
    }
    internal IReadOnlyList<string> Owners
    {
        get { var owners = new List<string>(); foreach (var lease in _listeners) owners.Add(lease.Owner); return owners.AsReadOnly(); }
    }
    internal void Publish()
    {
        if (_publishing) { Report(_name, "reentry", null); return; }
        _publishing = true;
        try
        {
            foreach (var lease in _listeners.ToArray())
            {
                if (!lease.Active) continue;
                try { lease.Callback(); }
                catch (Exception error) { Report(lease.Owner, "listener_failed", error); }
            }
        }
        finally { _publishing = false; }
    }
    private void Report(string owner, string code, Exception error)
    {
        try { _report?.Invoke(owner, "event." + _name, code, error); } catch { }
    }
    internal void Clear()
    {
        foreach (var lease in _listeners.ToArray()) lease.Dispose();
    }
    private sealed class Lease : IDisposable
    {
        private ExtensionEvent _event;
        internal string Owner { get; }
        internal Action Callback { get; private set; }
        internal bool Active => _event != null;
        internal Lease(ExtensionEvent ownerEvent, string owner, Action callback) { _event = ownerEvent; Owner = owner; Callback = callback; }
        public void Dispose()
        {
            var ownerEvent = _event;
            if (ownerEvent == null) return;
            _event = null; Callback = null;
            ownerEvent._listeners.Remove(this);
        }
    }
}
