#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

// Adapted for Clipsa by Francisco Serrano Jiménez, 2026-11-16.
// ShareX v22.4.0, ScrollingCaptureManager.StartCapture and HelpersLib/Input/InputManager.
// Only scroll input is included. Added target/process checks; no arbitrary key or text input.
using System.Drawing;
using System.Runtime.InteropServices;

namespace Clipsa;

internal enum ScrollInputMethod { MouseWheel, DownArrow, PageDown, ScrollMessage }

internal sealed class ScrollDriver
{
    private readonly IntPtr window, content;
    private readonly Rectangle region;
    private readonly Point point;
    private readonly uint process;
    private readonly CancellationToken cancellation;
    public ScrollDriver(IntPtr window, IntPtr content, Rectangle region, CancellationToken cancellation)
    {
        this.window = window; this.content = content; this.region = region; this.cancellation = cancellation;
        point = new Point(region.Left + region.Width / 2, region.Top + region.Height / 2);
        GetWindowThreadProcessId(window, out process);
    }
    public void Focus() { if (!cancellation.IsCancellationRequested && IsWindow(window)) SetForegroundWindow(window); }
    public void FocusForAutomatic() { Focus(); ValidateTarget(); SetCursorPos(point.X, point.Y); }
    private void ValidateTarget()
    {
        cancellation.ThrowIfCancellationRequested();
        GetWindowThreadProcessId(window, out var current);
        if (!IsWindow(window) || process != current) throw new InvalidOperationException("La ventana seleccionada ya no está disponible.");
        if (GetAncestor(GetForegroundWindow(), 2) != window) throw new InvalidOperationException("Has cambiado de ventana. El avance se ha detenido.");
    }
    public void Step(ScrollInputMethod method)
    {
        ValidateTarget();
        if (!region.Contains(System.Windows.Forms.Cursor.Position)) throw new InvalidOperationException("El ratón salió del área. Vuelve a pulsar Automático para continuar.");
        foreach (var key in new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C })
            if ((GetAsyncKeyState(key) & 0x8000) != 0) throw new InvalidOperationException("Suelta las teclas modificadoras y vuelve a iniciar el avance.");
        if (method == ScrollInputMethod.MouseWheel)
        {
            var input = new Input { Data = new InputData { Mouse = new MouseInput { Flags = 0x800, MouseData = unchecked((uint)-120) } } };
            Send(new[] { input });
        }
        else if (method is ScrollInputMethod.DownArrow or ScrollInputMethod.PageDown)
        {
            ushort key = method == ScrollInputMethod.DownArrow ? (ushort)0x28 : (ushort)0x22;
            Send(new[] { new Input { Type = 1, Data = new InputData { Keyboard = new KeyInput { VirtualKey = key } } }, new Input { Type = 1, Data = new InputData { Keyboard = new KeyInput { VirtualKey = key, Flags = 2 } } } });
        }
        else if (SendMessageTimeout(window, 0x115, new IntPtr(1), IntPtr.Zero, 2, 200, out _) == IntPtr.Zero)
            throw new InvalidOperationException("La ventana no respondió al mensaje de scroll.");
    }
    public void ForwardInitial(int delta, int key)
    {
        ValidateTarget();
        if (key is 0x26 or 0x28 or 0x21 or 0x22)
        {
            var thread = GetWindowThreadProcessId(window, out _);
            var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
            var destination = GetGUIThreadInfo(thread, ref info) && GetAncestor(info.Focus, 2) == window ? info.Focus : content;
            var bits = 1u | MapVirtualKey((uint)key, 0) << 16 | 0x01000000u;
            PostMessage(destination, 0x100, new IntPtr(key), new IntPtr(unchecked((int)bits)));
            PostMessage(destination, 0x101, new IntPtr(key), new IntPtr(unchecked((int)(bits | 0xC0000000u))));
        }
        else if (delta != 0)
        {
            var coordinates = new IntPtr(unchecked((point.Y & 0xFFFF) << 16 | (point.X & 0xFFFF)));
            if (SendMessageTimeout(content, 0x20A, new IntPtr(unchecked(delta << 16)), coordinates, 2, 200, out _) == IntPtr.Zero)
                throw new InvalidOperationException("No se pudo enviar el primer movimiento.");
        }
    }
    private static void Send(Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new InvalidOperationException("Windows no aceptó el movimiento. Prueba otro método o usa el modo manual.");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo { public uint Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint key, uint mode);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out UIntPtr result);
}
