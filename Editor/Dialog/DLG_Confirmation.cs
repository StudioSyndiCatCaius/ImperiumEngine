using System.Numerics;
using ImGuiNET;

namespace Editor.Dialog;

// a reusable yes/no modal. Fire-and-forget: DLG_Confirmation.Ask(...) queues it and invokes
// the matching callback once the user answers (or closes it, which counts as "no").
public class DLG_Confirmation : EditorDialog
{
    string _title = "Confirm";
    string _message = "";
    string _yes = "Yes";
    string _no = "No";
    Action? _on_yes;
    Action? _on_no;
    bool _answered;   // guards the "closed = no" fallback from double-firing

    public override string Title => _title;

    public static void Ask(string title, string message, Action on_yes, Action? on_no = null,
                           string yes = "Yes", string no = "No")
    {
        new DLG_Confirmation
        {
            _title = title, _message = message,
            _on_yes = on_yes, _on_no = on_no,
            _yes = yes, _no = no,
        }.Show();
    }

    protected override void OnDraw(double delta, EEditorWidgetDrawFlags flags)
    {
        ImGui.TextWrapped(_message);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button(_yes, new Vector2(120, 0)))
        {
            _answered = true;
            var cb = _on_yes;
            Dismiss();
            cb?.Invoke();
        }
        ImGui.SameLine();
        if (ImGui.Button(_no, new Vector2(120, 0)))
        {
            _answered = true;
            var cb = _on_no;
            Dismiss();
            cb?.Invoke();
        }
    }

    // closing via the X (without pressing a button) is treated as "no"
    protected override void OnClose()
    {
        if (!_answered) _on_no?.Invoke();
    }
}
