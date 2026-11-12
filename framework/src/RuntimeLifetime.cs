using System;
using System.Collections.Generic;

namespace ProbablyStolenModTemplate;

// One enabled session. Register releases before acquisition to cover partial failures.
internal sealed class RuntimeLifetime
{
    private enum Phase { Stopped, Starting, Running, Stopping, Faulted }
    private readonly List<Action> _releases = new List<Action>();
    private readonly Action<Exception> _report;
    private Phase _phase;
    internal bool IsFaulted => _phase == Phase.Faulted;

    public RuntimeLifetime(Action<Exception> report) { _report = report; }

    public bool Start(Action initialize)
    {
        if (_phase == Phase.Faulted) throw new InvalidOperationException("运行期清理失败，不能在未确认资源已释放时重新启用；请重启游戏");
        if (_phase != Phase.Stopped) return false;
        _phase = Phase.Starting;
        try
        {
            initialize();
            if (_phase != Phase.Starting) throw new InvalidOperationException("Runtime stopped during initialization.");
            _phase = Phase.Running;
            return true;
        }
        catch { Stop(); throw; }
    }

    public void Own(Action acquire, Action release)
    {
        if (_phase != Phase.Starting) throw new InvalidOperationException("Resources must be acquired during startup.");
        _releases.Add(release ?? throw new ArgumentNullException(nameof(release)));
        acquire();
    }

    public void Continue(Action initialize)
    {
        if (_phase != Phase.Running) throw new InvalidOperationException("Runtime is not awaiting a startup phase.");
        _phase = Phase.Starting;
        try
        {
            initialize();
            if (_phase != Phase.Starting) throw new InvalidOperationException("Runtime stopped during initialization.");
            _phase = Phase.Running;
        }
        catch { Stop(); throw; }
    }

    public void Stop()
    {
        if (_phase == Phase.Stopped || _phase == Phase.Stopping || _phase == Phase.Faulted) return;
        _phase = Phase.Stopping;
        bool failed = false;
        for (int i = _releases.Count - 1; i >= 0; i--)
        {
            try { _releases[i](); }
            catch (Exception error) { failed = true; try { _report?.Invoke(error); } catch { } }
        }
        _releases.Clear();
        // A release may have partially succeeded. Do not blindly replay it or restart over it.
        _phase = failed ? Phase.Faulted : Phase.Stopped;
    }
}
