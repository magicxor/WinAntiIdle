using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace WinAntiIdle;

public class Program
{
    private static readonly TimeSpan MaxIdleTime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(5);
    private static readonly TimeProvider TimeProvider = TimeProvider.System;
    private static readonly INPUT[] Inputs = GetInputs();
    private static readonly int SizeOfInput = Marshal.SizeOf<INPUT>();
    private static readonly uint SizeOfLastInputInfo = (uint)Marshal.SizeOf<LASTINPUTINFO>();

    private static INPUT[] GetInputs()
    {
        var input1 = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
        };
        input1.Anonymous.ki.wVk = VIRTUAL_KEY.VK_GAMEPAD_LEFT_THUMBSTICK_LEFT;
        input1.Anonymous.ki.time = 0;
        input1.Anonymous.ki.dwExtraInfo = (nuint)(nint)PInvoke.GetMessageExtraInfo();

        var input2 = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
        };
        input2.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        input2.Anonymous.ki.wVk = VIRTUAL_KEY.VK_GAMEPAD_LEFT_THUMBSTICK_LEFT;
        input2.Anonymous.ki.time = 0;
        input2.Anonymous.ki.dwExtraInfo = (nuint)(nint)PInvoke.GetMessageExtraInfo();

        return [input1, input2];
    }

    public static async Task Main(string[] args)
    {
        var cancelTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, consoleCancelEventArgs) =>
        {
            cancelTokenSource.Cancel();
            consoleCancelEventArgs.Cancel = true;
        };

        var timer = new PeriodicTimer(CheckPeriod, TimeProvider);

        while (!cancelTokenSource.Token.IsCancellationRequested
               && await timer.WaitForNextTickAsync(cancelTokenSource.Token))
        {
            var lastInputInfo = new LASTINPUTINFO
            {
                cbSize = SizeOfLastInputInfo,
            };
            var getLastInputInfoSucceeds = PInvoke.GetLastInputInfo(ref lastInputInfo);
            if (getLastInputInfoSucceeds)
            {
                var tickCountSinceBoot = PInvoke.GetTickCount();
                var lastInputTickCountSinceBoot = lastInputInfo.dwTime;
                var idleTime = TimeSpan.FromMilliseconds(tickCountSinceBoot - lastInputTickCountSinceBoot);
                if (idleTime > MaxIdleTime)
                {
                    Console.WriteLine($"Idle time is {idleTime}, sending input");

                    var insertedEventsCount = PInvoke.SendInput(Inputs.AsSpan(), SizeOfInput);

                    Console.WriteLine(insertedEventsCount == Inputs.Length
                        ? "Input sent successfully"
                        : $"Failed to send input, inserted {insertedEventsCount} out of {Inputs.Length}");
                }
                else
                {
                    Console.WriteLine($"Idle time is {idleTime}, not sending input");
                }
            }
            else
            {
                Console.WriteLine("Failed to get last input info");
            }
        }
    }
}
