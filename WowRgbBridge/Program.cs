// using System.Collections.Generic;
// using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
//using System.Globalization;
using System.Runtime.InteropServices;
using WowRgbBridge;

var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
var config = Config.LoadConfig(configPath);


// Initialize OpenRGB client and discover keyboard led devices.
Console.WriteLine("Discovering OpenRGB devices...");
using var keyboardManager = new Keyboard();
Console.WriteLine($"Discovered {keyboardManager.Devices.Length} OpenRGB devices.");
var keyboardIdx = keyboardManager.FindDeviceIndex(config.KeyboardName);
keyboardManager.SetDefaultDevice(config.KeyboardName);
var devices = keyboardManager.Devices;
Dictionary<string, int> led_indices = [];


// Setup screen reader
var pixelReader = new ScreenPixelReader(config.SignatureColors, config.SignatureColorTolerance);

// initialize squares to null to enter the discovery portion of the loop on first run
ScreenSquare[]? squares = null;

while (true)
// Main loop:
{
    // Check for if the foreground process matches config.foregroundProcessName, if not, sleep for 1 second and continue.
    var foregroundProcessName = GetForegroundProcessName();
    if (string.Equals(foregroundProcessName, config.ForegroundProcessName, StringComparison.OrdinalIgnoreCase))
    {
        // We found the desired process, check for color bar on the screen and read the colors.
        //Console.WriteLine($"Foreground process match \"{foregroundProcessName ?? "unknown"}\". Sleeping for 1 second.");

        if (squares is not null) // squares is not null, so we have previously found the marker sequence and can read the colors from the known squares on the screen.
        {
            //Console.WriteLine("Reading the colors from the known squares on the screen...");
            var input_colors = pixelReader.ReadSquares(squares);
            List<Keyboard.KeyUpdateData> ledUpdateData = [];
            if (input_colors is not null)
            {
                for (int i = 0; i < config.LedMappings.Count; i++)
                {
                    var led_name = config.LedMappings[i];
                    if(i >= input_colors.Length - 4)
                    {
                        Console.WriteLine($"Not enough input colors to map LED \"{led_name}\". Skipping.");
                        continue;
                    }
                    //Console.WriteLine($"Mapping LED \"{led_name}\" to color ({input_colors[i+4].Color.Red}, {input_colors[i+4].Color.Green}, {input_colors[i+4].Color.Blue})");

                    // Build update data entry
                    Color ledColor = Keyboard.ParseColorFromRgb(input_colors[i+4].Color.Red, input_colors[i+4].Color.Green, input_colors[i+4].Color.Blue);
                    Keyboard.KeyUpdateData ledData = new()
                    {
                        keyName = led_name,
                        color = ledColor
                    };
                    // Add update data to list
                    ledUpdateData.Add(ledData);
                }

                // Send to keyboard for LED update
                await keyboardManager.UpdateDeviceColorsAsync(ledUpdateData);

                // Update Status Key
                if (config.StatusLed != null) await keyboardManager.UpdateLed(config.StatusLed, Color.FromArgb(0,255,0));
                
            } else
            {
                Console.WriteLine("Marker sequence was lost. Resetting screen position...");
                squares = null;

                if (config.StatusLed != null) await keyboardManager.UpdateLed(config.StatusLed, Color.FromArgb(255,0,0));

            }
            Thread.Sleep(config.UpdateFrequencyMs);
            continue;
        } else // squares is null, so we need to find the marker sequence on the screen.
        {
            Console.WriteLine("No marker sequence found on the screen. Scanning for the marker sequence...");
            squares = pixelReader.FindSquares("screencap.bmp");   // Finds the pixel bar on the screen based on the four marker colors

            if(squares != null) {
                Console.WriteLine("Found sequence...");
                await keyboardManager.SetDefaultKeyColor(Keyboard.ParseColorFromHex(config.defaultKeyColor));
                await keyboardManager.SetStaticLeds(config.StaticLeds);
                continue;
            } else
            {
                // If cannot find sequence then sleep for 1 sec
                Thread.Sleep(1000);
                await keyboardManager.SetDefaultKeyColor(Keyboard.ParseColorFromHex(config.defaultKeyColor));
                if (config.StatusLed != null) await keyboardManager.UpdateLed(config.StatusLed, Color.FromArgb(255,0,0));
            }

        }

    } else
    {
        Console.WriteLine($"Foreground process is \"{foregroundProcessName ?? "unknown"}\", not \"{config.ForegroundProcessName}\". Sleeping for 1 second.");
        //await Task.Delay(1000);
        await keyboardManager.SetDefaultKeyColor(Keyboard.ParseColorFromHex(config.defaultKeyColor));
        //if (config.StatusLed != null) await keyboardManager.UpdateLed(config.StatusLed, Color.FromArgb(255,0,0));
        await Task.Delay(1000);
    }
    

}


static string? GetForegroundProcessName()
{
    // Return the foreground window's process name, or null if no process can be resolved.
    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException("Foreground process detection requires Windows.");
    }

    var windowHandle = NativeMethods.GetForegroundWindow();
    if (windowHandle == IntPtr.Zero
        || NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId) == 0)
    {
        return null;
    }

    try
    {
        using var process = Process.GetProcessById(checked((int)processId));
        return process.ProcessName;
    }
    catch (ArgumentException)
    {
        return null;
    }
}


static class NativeMethods
{
    // Obtain the current foreground window handle.
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern IntPtr GetForegroundWindow();

    // Resolve the process ID that owns a window.
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    // Read a system metric such as the primary-screen dimensions.
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern int GetSystemMetrics(int index);

    // Acquire a device context for the desktop screen.
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr windowHandle);

    // Release a device context acquired from the desktop.
    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

    // Release a memory device context.
    [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr deviceContext);
}