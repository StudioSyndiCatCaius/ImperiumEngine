using System.Collections;
using System.Reflection;
using Engine.Assets;
using Engine.Core;
using Engine.Interfaces;

namespace Engine.Structs;

public class THistory
{
    public List<THistoryItem> items = new();
    int index = -1;
    const int cap = 256;
    List<THistoryItem>? batch;
    public bool applying;

    public bool CanUndo => index >= 0;
    public bool CanRedo => index + 1 < items.Count;

    public void Clear()
    {
        items.Clear();
        index = -1;
        batch = null;
    }

    public void Begin()
    {
        if (applying) return;
        batch ??= new();
    }

    public void End()
    {
        if (batch == null) return;
        List<THistoryItem> group = batch;
        batch = null;
        if (group.Count == 0) return;
        if (group.Count == 1) Push(group[0]);
        else Push(new THistoryItem { children = group });
    }

    public void Record(object target, string path, object? old_value, object? new_value)
    {
        if (applying || target == null || string.IsNullOrEmpty(path)) return;
        if (Equals(old_value, new_value)) return;
        Push(new THistoryItem
        {
            target = target,
            path = path,
            old_value = old_value,
            new_value = new_value,
        });
    }

    public void Record(Action undo, Action redo)
    {
        if (applying || undo == null || redo == null) return;
        Push(new THistoryItem { undo = undo, redo = redo });
    }

    void Push(THistoryItem item)
    {
        if (batch != null)
        {
            batch.Add(item);
            return;
        }
        if (index + 1 < items.Count)
            items.RemoveRange(index + 1, items.Count - index - 1);
        items.Add(item);
        if (items.Count > cap)
        {
            items.RemoveAt(0);
            if (index > 0) index--;
        }
        index = items.Count - 1;
    }

    public void Undo()
    {
        if (!CanUndo) return;
        applying = true;
        items[index].Apply(false);
        index--;
        applying = false;
    }

    public void Redo()
    {
        if (!CanRedo) return;
        applying = true;
        index++;
        items[index].Apply(true);
        applying = false;
    }
}

public class THistoryItem
{
    public object? target;
    public string path = "";
    public object? old_value;
    public object? new_value;
    public Action? undo;
    public Action? redo;
    public List<THistoryItem>? children;

    public void Apply(bool to_new)
    {
        if (children != null && children.Count > 0)
        {
            if (to_new)
            {
                foreach (THistoryItem c in children) c.Apply(true);
            }
            else
            {
                for (int i = children.Count - 1; i >= 0; i--)
                    children[i].Apply(false);
            }
            return;
        }
        if (undo != null || redo != null)
        {
            if (to_new) redo?.Invoke();
            else undo?.Invoke();
            return;
        }
        SetPath(target, path, to_new ? new_value : old_value);
        if (target is A_Environment env) env.Refresh(true);
        if (target is I_Inspectable inspectable)
            inspectable.Inspectable_OnPropertyEdit(path,
                (to_new ? old_value : new_value)!,
                (to_new ? new_value : old_value)!);
    }

    static readonly BindingFlags FieldFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static void SetPath(object? target, string path, object? value)
    {
        if (target == null || string.IsNullOrEmpty(path)) return;
        if (target is Imp3D o3 && path == "transform" && value is TTransform3 t3)
        {
            o3.Transform_Set(t3, false);
            return;
        }

        string[] parts = path.Split('.');
        object? cur = target is Type ? null : target;
        Type type = target as Type ?? target.GetType();
        List<(object? owner, FieldInfo? field, IList? list, int index, object boxed)> backs = new();

        for (int i = 0; i < parts.Length; i++)
        {
            bool last = i == parts.Length - 1;
            string part = parts[i];

            if (cur is IList list && int.TryParse(part, out int idx))
            {
                if (idx < 0 || idx >= list.Count) return;
                if (last)
                {
                    list[idx] = value;
                    WriteBack(backs);
                    return;
                }
                object? next = list[idx];
                if (next == null) return;
                if (next.GetType().IsValueType)
                    backs.Add((null, null, list, idx, next));
                else
                    backs.Clear();
                cur = next;
                type = next.GetType();
                continue;
            }

            FieldInfo? field = type.GetField(part, FieldFlags);
            if (field == null) return;
            object? owner = field.IsStatic ? null : cur;
            if (last)
            {
                field.SetValue(owner, value);
                WriteBack(backs);
                return;
            }
            object? next_obj = field.GetValue(owner);
            if (next_obj == null) return;
            if (field.FieldType.IsValueType)
                backs.Add((owner, field, null, -1, next_obj));
            else
                backs.Clear();
            cur = next_obj;
            type = next_obj.GetType();
        }
    }

    static void WriteBack(List<(object? owner, FieldInfo? field, IList? list, int index, object boxed)> backs)
    {
        for (int i = backs.Count - 1; i >= 0; i--)
        {
            var b = backs[i];
            if (b.list != null) b.list[b.index] = b.boxed;
            else b.field?.SetValue(b.owner, b.boxed);
        }
    }
}
