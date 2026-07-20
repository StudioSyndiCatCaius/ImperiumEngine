using System.Numerics;
using ImGuiNET;

namespace Editor.Dialog;

// a modal progress dialog for a long-running background job (currently: saving assets).
//
// Run(...) queues the dialog and kicks the work off on a thread-pool task. The job reports its
// progress through a callback (fraction 0..1 + a status line); the dialog polls those values on
// the UI thread each frame and draws a progress bar. When the job finishes the dialog auto-closes
// (or, on failure, holds open showing the error until dismissed) and fires on_done(success).
public class DLG_Process : EditorDialog
{
    string _title = "Working…";

    readonly object _lock = new();   // guards the fields written by the worker, read by the UI
    float _progress;
    string _status = "";
    bool _done;
    bool _success;
    string _error = "";

    Action<bool>? _on_done;
    bool _fired;   // guards on_done + Dismiss from firing twice

    public override string Title => _title;

    // Runs `work` on a background thread. `work` reports progress via the supplied
    // report(fraction, status) callback and returns true on success. `on_done(success)` runs
    // on the UI thread once the dialog closes.
    public static void Run(string title, Func<Action<float, string>, bool> work, Action<bool>? on_done = null)
    {
        var dlg = new DLG_Process { _title = title, _on_done = on_done };
        dlg.Show();

        void Report(float fraction, string status)
        {
            lock (dlg._lock)
            {
                dlg._progress = Math.Clamp(fraction, 0f, 1f);
                dlg._status = status;
            }
        }

        Task.Run(() =>
        {
            bool ok = false;
            string err = "";
            try { ok = work(Report); }
            catch (Exception ex) { err = ex.Message; }

            lock (dlg._lock)
            {
                dlg._success = ok && err == "";
                dlg._error = err;
                dlg._done = true;
            }
        });
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        float progress; string status; bool done, success; string error;
        lock (_lock)
        {
            progress = _progress; status = _status;
            done = _done; success = _success; error = _error;
        }

        ImGui.TextUnformatted(status == "" ? "Working…" : status);
        ImGui.ProgressBar(done && success ? 1f : progress, new Vector2(360, 0));

        if (!done) return;

        if (!success)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f),
                error == "" ? "The operation failed (see log)." : error);
            ImGui.Separator();
            if (ImGui.Button("Close", new Vector2(120, 0)))
                Finish(false);
            return;
        }

        // success: close on the next frame after the full bar renders once
        Finish(true);
    }

    void Finish(bool success)
    {
        if (_fired) return;
        _fired = true;
        var cb = _on_done;
        Dismiss();
        cb?.Invoke(success);
    }
}
