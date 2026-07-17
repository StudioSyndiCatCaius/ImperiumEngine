using ImperiumCore.Classes.Components;
using ImperiumCore.Structs;

namespace ImperiumCore.Classes;

using System.Numerics;
using Silk.NET.Input;

// A single player: an input endpoint plus the UI focus it owns. ImpApp keeps a
// list of these and guarantees at least one always exists. Devices are polled
public class ImpPlayer
{
    public IInputContext InputContext;
    public ImpComponent2D FocusedWidget;

    private List<ImpDevice> Devices;

    private IMouse? _mouse;
    private IKeyboard? _keyboard;
    private bool _prevLeft;
    private bool _keyboardBound;

    public Vector2 MousePos { get; private set; }
    public bool LeftDown { get; private set; }
    public bool LeftPressed { get; private set; }    // went down this frame
    public bool LeftReleased { get; private set; }   // went up this frame

    public ImpComponent2D? Hovered     { get; private set; }
    public ImpComponent2D? PressTarget { get; private set; }

    public object?         DragPayload { get; private set; }
    public ImpComponent2D? DragSource  { get; private set; }
    public ImpComponent2D? DragTarget  { get; private set; }
    public bool            IsDragging  => DragSource != null;
    
    
    // -------------------------------------------------------------------------
    // Polling
    // -------------------------------------------------------------------------

    public void Input_Poll()
    {
        _mouse    ??= InputContext?.Mice.Count      > 0 ? InputContext.Mice[0]      : null;
        _keyboard ??= InputContext?.Keyboards.Count > 0 ? InputContext.Keyboards[0] : null;
        if (_mouse == null) return;

        if (_keyboard != null && !_keyboardBound)
        {
            _keyboard.KeyChar += (_, c)        => FocusedWidget?.OnKeyChar(c);
            _keyboard.KeyDown += (_, key, _sc) => FocusedWidget?.OnKeyDown(key);
            _keyboardBound = true;
        }

        MousePos = _mouse.Position;
        LeftDown = _mouse.IsButtonPressed(MouseButton.Left);
        LeftPressed = LeftDown && !_prevLeft;
        LeftReleased = !LeftDown && _prevLeft;
        _prevLeft = LeftDown;
    }

    // route hover / press / click / drag to the node under the cursor
    public void UI_Process(ImpComponent2D? target)
    {
        // Hover (always updated, even during drag)
        if (target != Hovered)
        {
            if (Hovered != null) { Hovered.hovered = false; Hovered.OnMouseExit(); }
            Hovered = target;
            if (Hovered != null) { Hovered.hovered = true; Hovered.OnMouseEnter(); }
        }

        if (IsDragging)
        {
            var dropTarget = Drag_FindTarget(target);
            if (dropTarget != DragTarget)
            {
                DragTarget?.OnDragExit(DragPayload);
                DragTarget = dropTarget;
                DragTarget?.OnDragEnter(DragPayload);
            }
            if (LeftReleased)
            {
                if (DragTarget != null)
                {
                    DragTarget.OnDrop(DragPayload);
                    DragTarget.OnDragExit(DragPayload);
                }
                DragSource?.OnDragEnd(DragTarget != null);
                DragSource = null; DragPayload = null; DragTarget = null;
            }
            return;
        }

        if (LeftPressed)
        {
            if (target != null)
            {
                PressTarget = target;
                target.pressed = true;
                target.OnMousePressed(0);
            }
            Focus_Set(target);
        }

        if (LeftReleased && PressTarget != null)
        {
            PressTarget.pressed = false;
            PressTarget.OnMouseReleased(0);
            if (PressTarget == target) target.OnClicked();
            PressTarget = null;
        }
    }

    // Walks up the component tree to find the nearest ancestor that accepts the payload.
    private ImpComponent2D? Drag_FindTarget(ImpComponent2D? hit)
    {
        ImpComponent? c = hit;
        while (c != null)
        {
            if (c is ImpComponent2D c2 && c2.drop_enabled && c2.OnDropCanAccept(DragPayload))
                return c2;
            c = c._parent;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Focus
    // -------------------------------------------------------------------------

    public void Drag_Begin(ImpComponent2D source, object? payload)
    {
        if (IsDragging) { DragSource?.OnDragEnd(false); DragSource = null; DragPayload = null; DragTarget = null; }
        DragSource  = source;
        DragPayload = payload;
    }

    public bool Focus_Set(ImpComponent2D? widget)
    {
        if (FocusedWidget == widget) return false;
        if (FocusedWidget != null) { FocusedWidget.focused = false; FocusedWidget.OnUnfocus(); }
        FocusedWidget = widget;
        if (FocusedWidget != null) { FocusedWidget.focused = true;  FocusedWidget.OnFocus();   }
        return true;
    }

    public void Focus_Clear()
    {
        Focus_Set(null);
    }


    // -------------------------------------------------------------------------
    // Single-key queries
    // -------------------------------------------------------------------------

    public bool Key_IsDown(TKey key)
    {
        return key.Device switch
        {
            EKeyDevice.Keyboard => _keyboard?.IsKeyPressed((Key)key.Code) ?? false,
            EKeyDevice.Mouse    => _mouse?.IsButtonPressed((MouseButton)key.Code) ?? false,
            _ => false,
        };
    }

    public bool Key_JustPressed(TKey key)
    {
        return false;
    }

    public Vector3 Key_GetAxis(TKey key)
    {

        return Key_IsDown(key) ? Vector3.UnitX : Vector3.Zero;
    }

    // -------------------------------------------------------------------------
    // Action (TInput) queries
    // -------------------------------------------------------------------------

    public bool Input_IsDown(TInput input)
    {

        return false;
    }

    public bool Input_JustPressed(TInput input)
    {

        return false;
    }

    public Vector3 Input_GetAxis(TInput input)
    {
        var result = Vector3.Zero;

        return result;
    }



}
