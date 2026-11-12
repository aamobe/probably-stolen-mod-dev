using System;
using System.Collections.Generic;
namespace ProbablyStolenModTemplate;

// Own actual event/update leases acquired during module enablement.
internal sealed class OwnedCallbacks
{
    private readonly List<Action> _releases = new List<Action>();
    private readonly Action<Exception> _report;
    private bool _released;
    internal bool ReleaseFailed { get; private set; }
    public OwnedCallbacks(Action<Exception> report) { _report = report; }

    public void Own(IDisposable lease)
    {
        RequireOpen();
        if (lease == null) throw new ArgumentNullException(nameof(lease));
        _releases.Add(lease.Dispose);
    }

    public void Release()
    {
        if (_released) return;
        _released = true;
        for (int i = _releases.Count - 1; i >= 0; i--)
            try { _releases[i](); }
            catch (Exception error) { ReleaseFailed = true; try { _report?.Invoke(error); } catch { } }
        _releases.Clear();
    }
    private void RequireOpen()
    {
        if (_released) throw new InvalidOperationException("回调所有权集合已经释放，不能重新获取资源");
    }
}
