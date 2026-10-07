using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TitanMDM.RemoteHost.Input;

public sealed class RemoteInputController
{
    private const uint InputMouse =
        0;

    private const uint InputKeyboard =
        1;

    private const uint MouseMove =
        0x0001;

    private const uint MouseLeftDown =
        0x0002;

    private const uint MouseLeftUp =
        0x0004;

    private const uint MouseRightDown =
        0x0008;

    private const uint MouseRightUp =
        0x0010;

    private const uint MouseWheel =
        0x0800;

    private const uint MouseAbsolute =
        0x8000;

    private const uint MouseVirtualDesk =
        0x4000;

    private const uint KeyboardKeyUp =
        0x0002;

    /*
     * ============================================================
     * POINTER
     * ============================================================
     */

    public void MovePointer(
        double normalizedX,
        double normalizedY,
        Rectangle targetMonitor)
    {
        var virtualScreen =
            SystemInformation.VirtualScreen;

        var clampedX =
            Math.Clamp(
                normalizedX,
                0d,
                1d);

        var clampedY =
            Math.Clamp(
                normalizedY,
                0d,
                1d);

        /*
         * Coordenada real dentro del monitor seleccionado.
         */
        var pixelX =
            targetMonitor.Left +
            (
                clampedX *
                Math.Max(
                    1,
                    targetMonitor.Width - 1)
            );

        var pixelY =
            targetMonitor.Top +
            (
                clampedY *
                Math.Max(
                    1,
                    targetMonitor.Height - 1)
            );

        /*
         * SendInput con MOUSEEVENTF_VIRTUALDESK utiliza
         * coordenadas 0..65535 relativas a todo el escritorio
         * virtual.
         */
        var x =
            (int)Math.Round(
                (
                    pixelX -
                    virtualScreen.Left
                )
                /
                Math.Max(
                    1d,
                    virtualScreen.Width - 1d)
                *
                65535d);

        var y =
            (int)Math.Round(
                (
                    pixelY -
                    virtualScreen.Top
                )
                /
                Math.Max(
                    1d,
                    virtualScreen.Height - 1d)
                *
                65535d);

        x =
            Math.Clamp(
                x,
                0,
                65535);

        y =
            Math.Clamp(
                y,
                0,
                65535);

        SendMouse(
            x,
            y,
            0,
            MouseMove |
            MouseAbsolute |
            MouseVirtualDesk);
    }

    public void LeftDown()
    {
        SendMouse(
            0,
            0,
            0,
            MouseLeftDown);
    }

    public void LeftUp()
    {
        SendMouse(
            0,
            0,
            0,
            MouseLeftUp);
    }

    public void RightDown()
    {
        SendMouse(
            0,
            0,
            0,
            MouseRightDown);
    }

    public void RightUp()
    {
        SendMouse(
            0,
            0,
            0,
            MouseRightUp);
    }

    public void Wheel(
        int delta)
    {
        SendMouse(
            0,
            0,
            unchecked(
                (uint)delta),
            MouseWheel);
    }

    /*
     * ============================================================
     * KEYBOARD
     * ============================================================
     */

    public void KeyDown(
        ushort virtualKey)
    {
        SendKeyboard(
            virtualKey,
            0);
    }

    public void KeyUp(
        ushort virtualKey)
    {
        SendKeyboard(
            virtualKey,
            KeyboardKeyUp);
    }

    /*
     * ============================================================
     * NATIVE INPUT
     * ============================================================
     */

    private static void SendMouse(
        int dx,
        int dy,
        uint mouseData,
        uint flags)
    {
        var input =
            new NativeInput
            {
                Type =
                    InputMouse,

                Data =
                    new InputUnion
                    {
                        Mouse =
                            new MouseInput
                            {
                                Dx =
                                    dx,

                                Dy =
                                    dy,

                                MouseData =
                                    mouseData,

                                Flags =
                                    flags,

                                Time =
                                    0,

                                ExtraInfo =
                                    IntPtr.Zero
                            }
                    }
            };

        Send(
            input);
    }

    private static void SendKeyboard(
        ushort virtualKey,
        uint flags)
    {
        var input =
            new NativeInput
            {
                Type =
                    InputKeyboard,

                Data =
                    new InputUnion
                    {
                        Keyboard =
                            new KeyboardInput
                            {
                                VirtualKey =
                                    virtualKey,

                                ScanCode =
                                    0,

                                Flags =
                                    flags,

                                Time =
                                    0,

                                ExtraInfo =
                                    IntPtr.Zero
                            }
                    }
            };

        Send(
            input);
    }

    private static void Send(
        NativeInput input)
    {
        var inputs =
            new[]
            {
                input
            };

        var sent =
            SendInput(
                1,
                inputs,
                Marshal.SizeOf<
                    NativeInput>());

        if (
            sent ==
            1)
        {
            return;
        }

        throw new Win32Exception(
            Marshal.GetLastWin32Error(),
            "Windows rechazó el evento de entrada remota.");
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern uint
        SendInput(
            uint numberOfInputs,
            NativeInput[] inputs,
            int structureSize);

    [StructLayout(
        LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;

        public InputUnion Data;
    }

    [StructLayout(
        LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;

        public int Dy;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public IntPtr ExtraInfo;
    }

    [StructLayout(
        LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;

        public ushort ScanCode;

        public uint Flags;

        public uint Time;

        public IntPtr ExtraInfo;
    }
}