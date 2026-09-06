/*******************************************************************************************
*
*   Vendored from raylib-extras/rlImGui-cs (MIT), with the FontAwesome icon-font block
*   removed (not needed here) and retargeted to Twizzle.ImGui.NET's ImGuiNET namespace,
*   since the official rlImgui-cs NuGet package hard-depends on mainline ImGui.NET, which
*   is a different native module than what Twizzle.ImGuizmo.NET is built against.
*
*   Copyright (c) 2021 Jeffery Myers
*
********************************************************************************************/

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

using Raylib_cs;
using ImGuiNET;

namespace rlImGui_cs
{
    public static class rlImGui
    {
        internal static IntPtr ImGuiContext = IntPtr.Zero;

        private static ImGuiMouseCursor CurrentMouseCursor = ImGuiMouseCursor.COUNT;
        private static Dictionary<ImGuiMouseCursor, MouseCursor> MouseCursorMap = new Dictionary<ImGuiMouseCursor, MouseCursor>();
        private static Texture2D FontTexture;

        static Dictionary<KeyboardKey, ImGuiKey> RaylibKeyMap = new Dictionary<KeyboardKey, ImGuiKey>();

        internal static bool LastFrameFocused = false;

        internal static bool LastControlPressed = false;
        internal static bool LastShiftPressed = false;
        internal static bool LastAltPressed = false;
        internal static bool LastSuperPressed = false;

        internal static bool rlImGuiIsControlDown() { return Raylib.IsKeyDown(KeyboardKey.RightControl) || Raylib.IsKeyDown(KeyboardKey.LeftControl); }
        internal static bool rlImGuiIsShiftDown() { return Raylib.IsKeyDown(KeyboardKey.RightShift) || Raylib.IsKeyDown(KeyboardKey.LeftShift); }
        internal static bool rlImGuiIsAltDown() { return Raylib.IsKeyDown(KeyboardKey.RightAlt) || Raylib.IsKeyDown(KeyboardKey.LeftAlt); }
        internal static bool rlImGuiIsSuperDown() { return Raylib.IsKeyDown(KeyboardKey.RightSuper) || Raylib.IsKeyDown(KeyboardKey.LeftSuper); }

        public delegate void SetupUserFontsCallback(ImGuiIOPtr imGuiIo);

        /// <summary>
        /// Callback for cases where the user wants to install additional fonts.
        /// </summary>
        public static SetupUserFontsCallback? SetupUserFonts = null;

        /// <summary>
        /// Sets up ImGui, loads fonts and themes
        /// </summary>
        public static void Setup(bool darkTheme = true, bool enableDocking = false)
        {
            BeginInitImGui();

            if (darkTheme)
                ImGui.StyleColorsDark();
            else
                ImGui.StyleColorsLight();

            if (enableDocking)
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;

            EndInitImGui();
        }

        public static void BeginInitImGui()
        {
            MouseCursorMap = new Dictionary<ImGuiMouseCursor, MouseCursor>();

            LastFrameFocused = Raylib.IsWindowFocused();
            LastControlPressed = false;
            LastShiftPressed = false;
            LastAltPressed = false;
            LastSuperPressed = false;

            FontTexture.Id = 0;

            SetupKeymap();

            ImGuiContext = ImGui.CreateContext();
        }

        internal static void SetupKeymap()
        {
            if (RaylibKeyMap.Count > 0)
                return;

            RaylibKeyMap[KeyboardKey.Apostrophe] = ImGuiKey.Apostrophe;
            RaylibKeyMap[KeyboardKey.Comma] = ImGuiKey.Comma;
            RaylibKeyMap[KeyboardKey.Minus] = ImGuiKey.Minus;
            RaylibKeyMap[KeyboardKey.Period] = ImGuiKey.Period;
            RaylibKeyMap[KeyboardKey.Slash] = ImGuiKey.Slash;
            RaylibKeyMap[KeyboardKey.Zero] = ImGuiKey._0;
            RaylibKeyMap[KeyboardKey.One] = ImGuiKey._1;
            RaylibKeyMap[KeyboardKey.Two] = ImGuiKey._2;
            RaylibKeyMap[KeyboardKey.Three] = ImGuiKey._3;
            RaylibKeyMap[KeyboardKey.Four] = ImGuiKey._4;
            RaylibKeyMap[KeyboardKey.Five] = ImGuiKey._5;
            RaylibKeyMap[KeyboardKey.Six] = ImGuiKey._6;
            RaylibKeyMap[KeyboardKey.Seven] = ImGuiKey._7;
            RaylibKeyMap[KeyboardKey.Eight] = ImGuiKey._8;
            RaylibKeyMap[KeyboardKey.Nine] = ImGuiKey._9;
            RaylibKeyMap[KeyboardKey.Semicolon] = ImGuiKey.Semicolon;
            RaylibKeyMap[KeyboardKey.Equal] = ImGuiKey.Equal;
            RaylibKeyMap[KeyboardKey.A] = ImGuiKey.A;
            RaylibKeyMap[KeyboardKey.B] = ImGuiKey.B;
            RaylibKeyMap[KeyboardKey.C] = ImGuiKey.C;
            RaylibKeyMap[KeyboardKey.D] = ImGuiKey.D;
            RaylibKeyMap[KeyboardKey.E] = ImGuiKey.E;
            RaylibKeyMap[KeyboardKey.F] = ImGuiKey.F;
            RaylibKeyMap[KeyboardKey.G] = ImGuiKey.G;
            RaylibKeyMap[KeyboardKey.H] = ImGuiKey.H;
            RaylibKeyMap[KeyboardKey.I] = ImGuiKey.I;
            RaylibKeyMap[KeyboardKey.J] = ImGuiKey.J;
            RaylibKeyMap[KeyboardKey.K] = ImGuiKey.K;
            RaylibKeyMap[KeyboardKey.L] = ImGuiKey.L;
            RaylibKeyMap[KeyboardKey.M] = ImGuiKey.M;
            RaylibKeyMap[KeyboardKey.N] = ImGuiKey.N;
            RaylibKeyMap[KeyboardKey.O] = ImGuiKey.O;
            RaylibKeyMap[KeyboardKey.P] = ImGuiKey.P;
            RaylibKeyMap[KeyboardKey.Q] = ImGuiKey.Q;
            RaylibKeyMap[KeyboardKey.R] = ImGuiKey.R;
            RaylibKeyMap[KeyboardKey.S] = ImGuiKey.S;
            RaylibKeyMap[KeyboardKey.T] = ImGuiKey.T;
            RaylibKeyMap[KeyboardKey.U] = ImGuiKey.U;
            RaylibKeyMap[KeyboardKey.V] = ImGuiKey.V;
            RaylibKeyMap[KeyboardKey.W] = ImGuiKey.W;
            RaylibKeyMap[KeyboardKey.X] = ImGuiKey.X;
            RaylibKeyMap[KeyboardKey.Y] = ImGuiKey.Y;
            RaylibKeyMap[KeyboardKey.Z] = ImGuiKey.Z;
            RaylibKeyMap[KeyboardKey.Space] = ImGuiKey.Space;
            RaylibKeyMap[KeyboardKey.Escape] = ImGuiKey.Escape;
            RaylibKeyMap[KeyboardKey.Enter] = ImGuiKey.Enter;
            RaylibKeyMap[KeyboardKey.Tab] = ImGuiKey.Tab;
            RaylibKeyMap[KeyboardKey.Backspace] = ImGuiKey.Backspace;
            RaylibKeyMap[KeyboardKey.Insert] = ImGuiKey.Insert;
            RaylibKeyMap[KeyboardKey.Delete] = ImGuiKey.Delete;
            RaylibKeyMap[KeyboardKey.Right] = ImGuiKey.RightArrow;
            RaylibKeyMap[KeyboardKey.Left] = ImGuiKey.LeftArrow;
            RaylibKeyMap[KeyboardKey.Down] = ImGuiKey.DownArrow;
            RaylibKeyMap[KeyboardKey.Up] = ImGuiKey.UpArrow;
            RaylibKeyMap[KeyboardKey.PageUp] = ImGuiKey.PageUp;
            RaylibKeyMap[KeyboardKey.PageDown] = ImGuiKey.PageDown;
            RaylibKeyMap[KeyboardKey.Home] = ImGuiKey.Home;
            RaylibKeyMap[KeyboardKey.End] = ImGuiKey.End;
            RaylibKeyMap[KeyboardKey.CapsLock] = ImGuiKey.CapsLock;
            RaylibKeyMap[KeyboardKey.ScrollLock] = ImGuiKey.ScrollLock;
            RaylibKeyMap[KeyboardKey.NumLock] = ImGuiKey.NumLock;
            RaylibKeyMap[KeyboardKey.PrintScreen] = ImGuiKey.PrintScreen;
            RaylibKeyMap[KeyboardKey.Pause] = ImGuiKey.Pause;
            RaylibKeyMap[KeyboardKey.F1] = ImGuiKey.F1;
            RaylibKeyMap[KeyboardKey.F2] = ImGuiKey.F2;
            RaylibKeyMap[KeyboardKey.F3] = ImGuiKey.F3;
            RaylibKeyMap[KeyboardKey.F4] = ImGuiKey.F4;
            RaylibKeyMap[KeyboardKey.F5] = ImGuiKey.F5;
            RaylibKeyMap[KeyboardKey.F6] = ImGuiKey.F6;
            RaylibKeyMap[KeyboardKey.F7] = ImGuiKey.F7;
            RaylibKeyMap[KeyboardKey.F8] = ImGuiKey.F8;
            RaylibKeyMap[KeyboardKey.F9] = ImGuiKey.F9;
            RaylibKeyMap[KeyboardKey.F10] = ImGuiKey.F10;
            RaylibKeyMap[KeyboardKey.F11] = ImGuiKey.F11;
            RaylibKeyMap[KeyboardKey.F12] = ImGuiKey.F12;
            RaylibKeyMap[KeyboardKey.LeftShift] = ImGuiKey.LeftShift;
            RaylibKeyMap[KeyboardKey.LeftControl] = ImGuiKey.LeftCtrl;
            RaylibKeyMap[KeyboardKey.LeftAlt] = ImGuiKey.LeftAlt;
            RaylibKeyMap[KeyboardKey.LeftSuper] = ImGuiKey.LeftSuper;
            RaylibKeyMap[KeyboardKey.RightShift] = ImGuiKey.RightShift;
            RaylibKeyMap[KeyboardKey.RightControl] = ImGuiKey.RightCtrl;
            RaylibKeyMap[KeyboardKey.RightAlt] = ImGuiKey.RightAlt;
            RaylibKeyMap[KeyboardKey.RightSuper] = ImGuiKey.RightSuper;
            RaylibKeyMap[KeyboardKey.KeyboardMenu] = ImGuiKey.Menu;
            RaylibKeyMap[KeyboardKey.LeftBracket] = ImGuiKey.LeftBracket;
            RaylibKeyMap[KeyboardKey.Backslash] = ImGuiKey.Backslash;
            RaylibKeyMap[KeyboardKey.RightBracket] = ImGuiKey.RightBracket;
            RaylibKeyMap[KeyboardKey.Grave] = ImGuiKey.GraveAccent;
            RaylibKeyMap[KeyboardKey.Kp0] = ImGuiKey.Keypad0;
            RaylibKeyMap[KeyboardKey.Kp1] = ImGuiKey.Keypad1;
            RaylibKeyMap[KeyboardKey.Kp2] = ImGuiKey.Keypad2;
            RaylibKeyMap[KeyboardKey.Kp3] = ImGuiKey.Keypad3;
            RaylibKeyMap[KeyboardKey.Kp4] = ImGuiKey.Keypad4;
            RaylibKeyMap[KeyboardKey.Kp5] = ImGuiKey.Keypad5;
            RaylibKeyMap[KeyboardKey.Kp6] = ImGuiKey.Keypad6;
            RaylibKeyMap[KeyboardKey.Kp7] = ImGuiKey.Keypad7;
            RaylibKeyMap[KeyboardKey.Kp8] = ImGuiKey.Keypad8;
            RaylibKeyMap[KeyboardKey.Kp9] = ImGuiKey.Keypad9;
            RaylibKeyMap[KeyboardKey.KpDecimal] = ImGuiKey.KeypadDecimal;
            RaylibKeyMap[KeyboardKey.KpDivide] = ImGuiKey.KeypadDivide;
            RaylibKeyMap[KeyboardKey.KpMultiply] = ImGuiKey.KeypadMultiply;
            RaylibKeyMap[KeyboardKey.KpSubtract] = ImGuiKey.KeypadSubtract;
            RaylibKeyMap[KeyboardKey.KpAdd] = ImGuiKey.KeypadAdd;
            RaylibKeyMap[KeyboardKey.KpEnter] = ImGuiKey.KeypadEnter;
            RaylibKeyMap[KeyboardKey.KpEqual] = ImGuiKey.KeypadEqual;
        }

        private static void SetupMouseCursors()
        {
            MouseCursorMap.Clear();
            MouseCursorMap[ImGuiMouseCursor.Arrow] = MouseCursor.Arrow;
            MouseCursorMap[ImGuiMouseCursor.TextInput] = MouseCursor.IBeam;
            MouseCursorMap[ImGuiMouseCursor.Hand] = MouseCursor.PointingHand;
            MouseCursorMap[ImGuiMouseCursor.ResizeAll] = MouseCursor.ResizeAll;
            MouseCursorMap[ImGuiMouseCursor.ResizeEW] = MouseCursor.ResizeEw;
            MouseCursorMap[ImGuiMouseCursor.ResizeNESW] = MouseCursor.ResizeNesw;
            MouseCursorMap[ImGuiMouseCursor.ResizeNS] = MouseCursor.ResizeNs;
            MouseCursorMap[ImGuiMouseCursor.ResizeNWSE] = MouseCursor.ResizeNwse;
            MouseCursorMap[ImGuiMouseCursor.NotAllowed] = MouseCursor.NotAllowed;
        }

        /// <summary>
        /// Forces the font texture atlas to be recomputed and re-cached
        /// </summary>
        public static unsafe void ReloadFonts()
        {
            ImGui.SetCurrentContext(ImGuiContext);
            ImGuiIOPtr io = ImGui.GetIO();

            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int _);

            Raylib_cs.Image image = new Image
            {
                Data = pixels,
                Width = width,
                Height = height,
                Mipmaps = 1,
                Format = PixelFormat.UncompressedR8G8B8A8,
            };

            if (FontTexture.Id != 0)
                Raylib.UnloadTexture(FontTexture);

            FontTexture = Raylib.LoadTextureFromImage(image);

            io.Fonts.SetTexID(new IntPtr(FontTexture.Id));
        }

        unsafe internal static sbyte* rImGuiGetClipText(IntPtr userData) => Raylib.GetClipboardText();
        unsafe internal static void rlImGuiSetClipText(IntPtr userData, sbyte* text) => Raylib.SetClipboardText(text);

        private unsafe delegate sbyte* GetClipTextCallback(IntPtr userData);
        private unsafe delegate void SetClipTextCallback(IntPtr userData, sbyte* text);

        private static GetClipTextCallback GetClipCallback = null!;
        private static SetClipTextCallback SetClipCallback = null!;

        public static void EndInitImGui()
        {
            SetupMouseCursors();

            ImGui.SetCurrentContext(ImGuiContext);

            ImGuiIOPtr io = ImGui.GetIO();
            if (SetupUserFonts != null)
                SetupUserFonts(io);
            else
                io.Fonts.AddFontDefault();

            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos | ImGuiBackendFlags.HasGamepad;

            io.MousePos.X = 0;
            io.MousePos.Y = 0;

            unsafe
            {
                SetClipCallback = new SetClipTextCallback(rlImGuiSetClipText);
                io.SetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(SetClipCallback);

                GetClipCallback = new GetClipTextCallback(rImGuiGetClipText);
                io.GetClipboardTextFn = Marshal.GetFunctionPointerForDelegate(GetClipCallback);
            }

            io.ClipboardUserData = IntPtr.Zero;
            ReloadFonts();
        }

        private static void SetMouseEvent(ImGuiIOPtr io, MouseButton rayMouse, ImGuiMouseButton imGuiMouse)
        {
            if (Raylib.IsMouseButtonPressed(rayMouse))
                io.AddMouseButtonEvent((int)imGuiMouse, true);
            else if (Raylib.IsMouseButtonReleased(rayMouse))
                io.AddMouseButtonEvent((int)imGuiMouse, false);
        }

        private static void NewFrame(float dt = -1)
        {
            ImGuiIOPtr io = ImGui.GetIO();

            if (Raylib.IsWindowFullscreen())
            {
                int monitor = Raylib.GetCurrentMonitor();
                io.DisplaySize = new Vector2(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
            }
            else
            {
                io.DisplaySize = new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            }

            io.DisplayFramebufferScale = new Vector2(1, 1);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || Raylib.IsWindowState(ConfigFlags.HighDpiWindow))
                io.DisplayFramebufferScale = Raylib.GetWindowScaleDPI();

            io.DeltaTime = dt >= 0 ? dt : Raylib.GetFrameTime();

            if (io.WantSetMousePos)
                Raylib.SetMousePosition((int)io.MousePos.X, (int)io.MousePos.Y);
            else
                io.AddMousePosEvent(Raylib.GetMouseX(), Raylib.GetMouseY());

            SetMouseEvent(io, MouseButton.Left, ImGuiMouseButton.Left);
            SetMouseEvent(io, MouseButton.Right, ImGuiMouseButton.Right);
            SetMouseEvent(io, MouseButton.Middle, ImGuiMouseButton.Middle);
            SetMouseEvent(io, MouseButton.Forward, ImGuiMouseButton.Middle + 1);
            SetMouseEvent(io, MouseButton.Back, ImGuiMouseButton.Middle + 2);

            var wheelMove = Raylib.GetMouseWheelMoveV();
            io.AddMouseWheelEvent(wheelMove.X, wheelMove.Y);

            if ((io.ConfigFlags & ImGuiConfigFlags.NoMouseCursorChange) == 0)
            {
                ImGuiMouseCursor imgui_cursor = ImGui.GetMouseCursor();
                if (imgui_cursor != CurrentMouseCursor || io.MouseDrawCursor)
                {
                    CurrentMouseCursor = imgui_cursor;
                    if (io.MouseDrawCursor || imgui_cursor == ImGuiMouseCursor.None)
                    {
                        Raylib.HideCursor();
                    }
                    else
                    {
                        Raylib.ShowCursor();
                        if (!MouseCursorMap.ContainsKey(imgui_cursor))
                            Raylib.SetMouseCursor(MouseCursor.Default);
                        else
                            Raylib.SetMouseCursor(MouseCursorMap[imgui_cursor]);
                    }
                }
            }
        }

        private static void FrameEvents()
        {
            ImGuiIOPtr io = ImGui.GetIO();

            bool focused = Raylib.IsWindowFocused();
            if (focused != LastFrameFocused)
                io.AddFocusEvent(focused);
            LastFrameFocused = focused;

            // NewFrame rebuilds io.KeyCtrl from these reserved-mod events. Writing
            // KeyCtrl here is wiped, and GetKeyPressed() never emits modifiers, so
            // InputText chords (Ctrl+A/C/X/V/Z, Ctrl+Left/Right) would never fire.
            bool ctrlDown = rlImGuiIsControlDown();
            if (ctrlDown != LastControlPressed)
                io.AddKeyEvent(ImGuiKey.ReservedForModCtrl, ctrlDown);
            LastControlPressed = ctrlDown;
            bool shiftDown = rlImGuiIsShiftDown();
            if (shiftDown != LastShiftPressed)
                io.AddKeyEvent(ImGuiKey.ReservedForModShift, shiftDown);
            LastShiftPressed = shiftDown;
            bool altDown = rlImGuiIsAltDown();
            if (altDown != LastAltPressed)
                io.AddKeyEvent(ImGuiKey.ReservedForModAlt, altDown);
            LastAltPressed = altDown;
            bool superDown = rlImGuiIsSuperDown();
            if (superDown != LastSuperPressed)
                io.AddKeyEvent(ImGuiKey.ReservedForModSuper, superDown);
            LastSuperPressed = superDown;

            while (Raylib.GetKeyPressed() != 0) { }

            foreach (var keyItr in RaylibKeyMap)
            {
                if (Raylib.IsKeyReleased(keyItr.Key))
                    io.AddKeyEvent(keyItr.Value, false);
                else if (Raylib.IsKeyPressed(keyItr.Key))
                    io.AddKeyEvent(keyItr.Value, true);
            }

            var pressed = Raylib.GetCharPressed();
            while (pressed != 0)
            {
                io.AddInputCharacter((uint)pressed);
                pressed = Raylib.GetCharPressed();
            }
        }

        /// <summary>
        /// Starts a new ImGui Frame
        /// </summary>
        public static void Begin(float dt = -1)
        {
            ImGui.SetCurrentContext(ImGuiContext);

            NewFrame(dt);
            FrameEvents();
            ImGui.NewFrame();
        }

        private static void EnableScissor(float x, float y, float width, float height)
        {
            Rlgl.EnableScissorTest();
            ImGuiIOPtr io = ImGui.GetIO();

            Vector2 scale = new Vector2(1.0f, 1.0f);
            if (Raylib.IsWindowState(ConfigFlags.HighDpiWindow) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                scale = io.DisplayFramebufferScale;

            Rlgl.Scissor((int)(x * scale.X),
                         (int)((io.DisplaySize.Y - (int)(y + height)) * scale.Y),
                         (int)(width * scale.X),
                         (int)(height * scale.Y));
        }

        private static void TriangleVert(ImDrawVertPtr idx_vert)
        {
            Vector4 color = ImGui.ColorConvertU32ToFloat4(idx_vert.col);

            Rlgl.Color4f(color.X, color.Y, color.Z, color.W);
            Rlgl.TexCoord2f(idx_vert.uv.X, idx_vert.uv.Y);
            Rlgl.Vertex2f(idx_vert.pos.X, idx_vert.pos.Y);
        }

        private static void RenderTriangles(uint count, uint indexStart, ImVector<ushort> indexBuffer, ImPtrVector<ImDrawVertPtr> vertBuffer, IntPtr texturePtr)
        {
            if (count < 3)
                return;

            uint textureId = 0;
            if (texturePtr != IntPtr.Zero)
                textureId = (uint)texturePtr.ToInt32();

            Rlgl.Begin(DrawMode.Triangles);
            Rlgl.SetTexture(textureId);

            for (int i = 0; i <= (count - 3); i += 3)
            {
                if (Rlgl.CheckRenderBatchLimit(3))
                {
                    Rlgl.Begin(DrawMode.Triangles);
                    Rlgl.SetTexture(textureId);
                }

                ushort indexA = indexBuffer[(int)indexStart + i];
                ushort indexB = indexBuffer[(int)indexStart + i + 1];
                ushort indexC = indexBuffer[(int)indexStart + i + 2];

                TriangleVert(vertBuffer[indexA]);
                TriangleVert(vertBuffer[indexB]);
                TriangleVert(vertBuffer[indexC]);
            }
            Rlgl.End();
        }

        private delegate void Callback(ImDrawListPtr list, ImDrawCmdPtr cmd);

        private static void RenderData()
        {
            Rlgl.DrawRenderBatchActive();
            Rlgl.DisableBackfaceCulling();
            Rlgl.DisableDepthTest();

            var data = ImGui.GetDrawData();

            for (int l = 0; l < data.CmdListsCount; l++)
            {
                ImDrawListPtr commandList = data.CmdListsRange[l];

                for (int cmdIndex = 0; cmdIndex < commandList.CmdBuffer.Size; cmdIndex++)
                {
                    var cmd = commandList.CmdBuffer[cmdIndex];

                    EnableScissor(cmd.ClipRect.X - data.DisplayPos.X, cmd.ClipRect.Y - data.DisplayPos.Y,
                                  cmd.ClipRect.Z - (cmd.ClipRect.X - data.DisplayPos.X), cmd.ClipRect.W - (cmd.ClipRect.Y - data.DisplayPos.Y));

                    if (cmd.UserCallback != IntPtr.Zero)
                    {
                        Callback cb = Marshal.GetDelegateForFunctionPointer<Callback>(cmd.UserCallback);
                        cb(commandList, cmd);
                        continue;
                    }

                    RenderTriangles(cmd.ElemCount, cmd.IdxOffset, commandList.IdxBuffer, commandList.VtxBuffer, cmd.TextureId);

                    Rlgl.DrawRenderBatchActive();
                }
            }
            Rlgl.SetTexture(0);
            Rlgl.DisableScissorTest();
            Rlgl.EnableBackfaceCulling();
            Rlgl.EnableDepthTest();
        }

        /// <summary>
        /// Ends an ImGui frame and submits all ImGui drawing to raylib for processing.
        /// </summary>
        public static void End()
        {
            ImGui.SetCurrentContext(ImGuiContext);
            ImGui.Render();
            RenderData();
        }

        /// <summary>
        /// Cleanup ImGui and unload font atlas
        /// </summary>
        public static void Shutdown()
        {
            Raylib.UnloadTexture(FontTexture);
            ImGui.DestroyContext();
        }
    }
}
