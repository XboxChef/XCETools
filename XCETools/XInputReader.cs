// =============================================================================
// XInputReader.cs - Read the first connected PC Xbox-compatible gamepad
// =============================================================================
using System;
using System.Runtime.InteropServices;

namespace XCETools
{
    /// <summary>Thin wrapper around Windows XInput (player index 0).</summary>
    internal static class XInputReader
    {
        private const int Success = 0;
        private const int ErrorDeviceNotConnected = 0x48F;

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState4(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState9(int dwUserIndex, out XINPUT_STATE pState);

        public static bool TryRead(int userIndex, out ushort buttons, out byte leftTrigger, out byte rightTrigger,
            out short leftThumbX, out short leftThumbY, out short rightThumbX, out short rightThumbY)
        {
            buttons = leftTrigger = rightTrigger = 0;
            leftThumbX = leftThumbY = rightThumbX = rightThumbY = 0;

            int err = XInputGetState4(userIndex, out XINPUT_STATE state);
            if (err == ErrorDeviceNotConnected)
                err = XInputGetState9(userIndex, out state);
            if (err != Success)
                return false;

            var g = state.Gamepad;
            buttons = g.wButtons;
            leftTrigger = g.bLeftTrigger;
            rightTrigger = g.bRightTrigger;
            leftThumbX = g.sThumbLX;
            leftThumbY = g.sThumbLY;
            rightThumbX = g.sThumbRX;
            rightThumbY = g.sThumbRY;
            return true;
        }
    }
}
