using OpenRGB.NET;
using System.Globalization;

namespace WowRgbBridge
{
    public class Keyboard : IDisposable
    {
        private readonly OpenRgbClient _client;
        private List<Dictionary<string, int>> devicesLeds = [];
        private int _defaultDeviceIndex = 0;

        public Keyboard()
        {
            _client = CreateOpenRgbClient();
            try
            {
                Devices = _client.GetAllControllerData().ToArray();
                if (Devices.Length == 0)
                {
                    throw new InvalidOperationException("OpenRGB did not report any devices.");
                }
            }
            catch
            {
                _client.Dispose();
                throw;
            }

            foreach(int i in Enumerable.Range(0, Devices.Length))
            {
                Console.WriteLine($"Loading LED info for {i}: {Devices[i].Name} with {Devices[i].Leds.Length} LEDs.");
                var ledDict = new Dictionary<string, int>();
                foreach (var led in Devices[i].Leds)
                {
                    ledDict[led.Name] = led.Index;

                }
                devicesLeds.Add(ledDict);
            }

        }

        public Device[] Devices { get; }

        public void SetDefaultDevice(string namePrefix)
        // Set the default device index based on the name prefix of the device.
        {
            var deviceIndex = FindDeviceIndex(namePrefix);
            if (deviceIndex == -1)
            {
                throw new InvalidOperationException($"OpenRGB did not report a device with name starting with \"{namePrefix}\".");
            }
        
            _defaultDeviceIndex = deviceIndex;
        }

        public void SetDefaultDevice(int deviceIndex)
        // Set the default device index based on the index of the device.
        {
            if (deviceIndex < 0 || deviceIndex >= Devices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(deviceIndex), $"Device index {deviceIndex} is out of range. Valid range is 0 to {Devices.Length - 1}.");
            }
        
            _defaultDeviceIndex = deviceIndex;
        }

        public int FindDeviceIndex(string namePrefix)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(namePrefix);

            return Array.FindIndex(
                Devices,
                device => device.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase));
        }

        public static OpenRgbClient CreateOpenRgbClient()
        {
            return new OpenRgbClient("127.0.0.1", 6742, "WowRgbBridge");
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        public int? GetLedIndex(string ledName, int deviceIndex = -1)
        {
            if (deviceIndex == -1) // If no device index is provided, use the default device index.
            {
                deviceIndex = _defaultDeviceIndex;
            }
        
            if (deviceIndex < 0 || deviceIndex >= Devices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(deviceIndex), $"Device index {deviceIndex} is out of range. Valid range is 0 to {Devices.Length - 1}.");
            }

            if (DeviceExists(deviceIndex) && devicesLeds[deviceIndex].TryGetValue(ledName, out int ledIndex))
            {
                return ledIndex;
            }

            return null;
        }

        private bool DeviceExists(int deviceIndex)
        {
            return deviceIndex >=0 &&
                deviceIndex < devicesLeds.Count &&
                devicesLeds[deviceIndex] != null;
        }

        public async Task UpdateDeviceColorsAsync(List<KeyUpdateData> updateData, int deviceIndex = -1)
        {

            if (_client is null) // We shouldn't be here. _client should be initiated when class is constructed.
            {
                throw new InvalidOperationException("OpenRGB does not have an open client.");
            }

            if (deviceIndex == -1) // If no device index is provided, use the default device index.
            {
                deviceIndex = _defaultDeviceIndex;
            }

            foreach(KeyUpdateData data in updateData)
            {
                //Console.WriteLine($"Updating key {data.keyName} to color {data.color}");
                int? keyIndex = GetLedIndex(data.keyName, deviceIndex);

                if(keyIndex != null) {
                    _client.UpdateSingleLed(
                        deviceIndex,
                        keyIndex.Value,
                        GetColorFromSystemDrawingColor(data.color));
                    //await Task.Delay(TimeSpan.FromMilliseconds(5));
                } else
                {
                    Console.WriteLine($"Unable to find key index for {data.keyName} on device {deviceIndex}");
                }
            }

        }

        public async Task UpdateLed(string in_keyName, System.Drawing.Color in_color, int deviceIndex = -1) => 
            await UpdateDeviceColorsAsync([new KeyUpdateData { keyName = in_keyName, color = in_color }], deviceIndex);

        public async Task SetStaticLeds(Dictionary<string,string> defaultLedMap, int deviceIndex = -1)
        {
            List<Keyboard.KeyUpdateData> ledUpdateData = [];
            foreach(var led in defaultLedMap)
            {
                KeyUpdateData ledData = new()
                    {
                        keyName = led.Key,
                        color = ParseColorFromHex(led.Value)
                    };
                ledUpdateData.Add(ledData);
            }
            if(ledUpdateData.Count > 0)
            {
                await UpdateDeviceColorsAsync(ledUpdateData, deviceIndex);
            }
        }

        public async Task SetDefaultKeyColor(System.Drawing.Color defaultColor, int deviceIndex = -1)
        {
            if (_client is null) // We shouldn't be here. _client should be initiated when class is constructed.
            {
                throw new InvalidOperationException("OpenRGB does not have an open client.");
            }

            if (deviceIndex == -1) // If no device index is provided, use the default device index.
            {
                deviceIndex = _defaultDeviceIndex;
            }

            foreach (var led in devicesLeds[deviceIndex])
            {
                _client.UpdateSingleLed(
                        deviceIndex,
                        led.Value,
                        GetColorFromSystemDrawingColor(defaultColor));
            }
        }

        private Color GetColorFromSystemDrawingColor(System.Drawing.Color in_color)
        {
            return new Color(in_color.R, in_color.G, in_color.B);
        }

        public static System.Drawing.Color ParseColorFromRgb(int r, int g, int b)
            {
                if (r is < 0 or > 255 ||
                    g is < 0 or > 255 ||
                    b is < 0 or > 255)
                {
                    throw new ArgumentOutOfRangeException("Color values not between 0 and 255");
                }

                return System.Drawing.Color.FromArgb((byte)r, (byte)g, (byte)b);
            }

        public static System.Drawing.Color ParseColorFromHex(string color)
        {
            if (color is null
                || color.Length != 7
                || color[0] != '#'
                || !byte.TryParse(color.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
                || !byte.TryParse(color.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
                || !byte.TryParse(color.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            {
                throw new ArgumentException($"Invalid RGB marker color \"{color}\". Expected #RRGGBB.");
            }

            return System.Drawing.Color.FromArgb(red, green, blue);
        }

        public struct KeyUpdateData
        {
            public string keyName;
            public System.Drawing.Color color;
        }

    }

}

