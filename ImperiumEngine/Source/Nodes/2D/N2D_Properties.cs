using System.Numerics;
using System.Reflection;
using ImGuiNET;

namespace ImperiumEngine.Source.Nodes._2D;

public class N2D_Properties : ImpObject2D
{
    private const float SliderMin = -100.0f;
    private const float SliderMax = 100.0f;
    private object target = new ExampleObject();
    
    public override void OnDraw(double delta)
    {
        if (target == null) return;

        Type type = target.GetType();
        
        // Get all fields with Exposed attribute
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => Attribute.IsDefined(field, typeof(ExposedAttribute)));

        // Get all properties with Exposed attribute
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(prop => Attribute.IsDefined(prop, typeof(ExposedAttribute)));

        ImGui.Begin($"Properties: {type.Name}");

        // Draw fields
        foreach (var field in fields)
        {
            DrawMember(target, field.Name, field.FieldType,
                () => field.GetValue(target),
                (value) => field.SetValue(target, value));
        }

        // Draw properties
        foreach (var property in properties)
        {
            if (property.CanRead && property.CanWrite)
            {
                DrawMember(target, property.Name, property.PropertyType,
                    () => property.GetValue(target),
                    (value) => property.SetValue(target, value));
            }
        }

        ImGui.End();
    }

    private void DrawMember(object target, string name, Type memberType, Func<object> getValue, Action<object> setValue)
    {
        ImGui.PushID(name);
        ImGui.AlignTextToFramePadding();
        
        ImGui.Text(name);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        var value = getValue();
        var modified = false;

        if (memberType == typeof(float))
        {
            var floatValue = (float)value;
            if (ImGui.SliderFloat("##value", ref floatValue, SliderMin, SliderMax))
            {
                setValue(floatValue);
                modified = true;
            }
        }
        else if (memberType == typeof(string))
        {
            var strValue = (string)value ?? string.Empty;
            if (ImGui.InputText("##value", ref strValue, 256))
            {
                setValue(strValue);
                modified = true;
            }
        }
        else if (memberType == typeof(bool))
        {
            var boolValue = (bool)value;
            if (ImGui.Checkbox("##value", ref boolValue))
            {
                setValue(boolValue);
                modified = true;
            }
        }
        else
        {
            // For unsupported types, just display the value as text
            ImGui.Text(value?.ToString() ?? "null");
        }

        ImGui.PopID();
    }
}

// Example usage:
public class ExampleObject
{
    [Exposed]
    public float Speed = 10.0f;

    [Exposed]
    public string Name = "Entity";

    [Exposed]
    public bool IsActive = true;

    [Exposed]
    public Vector3 Position; // This will be displayed as text only since it's not a supported type
}