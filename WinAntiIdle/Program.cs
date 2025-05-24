using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace WinAntiIdle;

internal static class Program
{
    private static readonly TimeSpan MaxIdleTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(2);
    private static readonly TimeProvider TimeProvider = TimeProvider.System;
    private const VIRTUAL_KEY Key = VIRTUAL_KEY.VK__none_;
    private static readonly INPUT[] Inputs = GetInputs(Key);
    private static readonly int SizeOfInput = Marshal.SizeOf<INPUT>();
    private static readonly uint SizeOfLastInputInfo = (uint)Marshal.SizeOf<LASTINPUTINFO>();

    private static INPUT[] GetInputs(VIRTUAL_KEY key)
    {
        var input1 = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
        };
        input1.Anonymous.ki.wVk = key;
        input1.Anonymous.ki.time = 0;
        input1.Anonymous.ki.dwExtraInfo = (nuint)(nint)PInvoke.GetMessageExtraInfo();

        var input2 = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
        };
        input2.Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        input2.Anonymous.ki.wVk = key;
        input2.Anonymous.ki.time = 0;
        input2.Anonymous.ki.dwExtraInfo = (nuint)(nint)PInvoke.GetMessageExtraInfo();

        return [input1, input2];
    }

    public static async Task Main(string[] args)
    {
        using var cancelTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, consoleCancelEventArgs) =>
        {
            cancelTokenSource.Cancel();
            consoleCancelEventArgs.Cancel = true;
        };

        using var timer = new PeriodicTimer(CheckPeriod, TimeProvider);

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
                    Console.WriteLine($"Idle time is {(int)idleTime.TotalSeconds}s, sending input: {Key}");

                    var insertedEventsCount = PInvoke.SendInput(Inputs.AsSpan(), SizeOfInput);

                    Console.WriteLine(insertedEventsCount == Inputs.Length
                        ? "Input sent successfully"
                        : $"Failed to send input, inserted {insertedEventsCount} out of {Inputs.Length}");
                }
                else
                {
                    Console.WriteLine($"Idle time is {(int)idleTime.TotalSeconds}s, waiting...");
                }
            }
            else
            {
                Console.WriteLine("Failed to get last input info");
            }
        }
    }
}
