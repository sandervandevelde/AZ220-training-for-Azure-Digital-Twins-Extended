// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static CheeseCaveDevice.CheeseCaveSimulator;
using Microsoft.Extensions.Configuration;

namespace CheeseCaveDevice
{
    public sealed class Settings
    {
        public string cs { get; set; }
    }

    class Program
    {
        const int intervalInMilliseconds = 5000;                // Interval at which telemetry is sent to the cloud.

        // Global variables.
        private static DeviceClient deviceClient;

        private static CheeseCaveSimulator cheeseCave;
       
        private static void Main(string[] args)
        {
            ConsoleHelper.WriteColorMessage("Cheese Cave device app.\n", ConsoleColor.Yellow);

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            Settings settings = config.GetRequiredSection("Settings").Get<Settings>();

            // The device connection string to authenticate the device with your IoT hub.
            var deviceConnectionString = settings.cs; 

            // Connect to the IoT hub using the MQTT protocol.
            deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);

            // Create an instance of the Cheese Cave Simulator
            cheeseCave = new CheeseCaveSimulator();

            // INSERT register direct method code below here
            // Create a handler for the direct method call
            deviceClient.SetMethodHandlerAsync("SetFanState", SetFanState, null).Wait();

            // INSERT register desired property changed handler code below here
            // Get the device twin to report the initial desired properties.
            Twin deviceTwin = deviceClient.GetTwinAsync().GetAwaiter().GetResult();
            ConsoleHelper.WriteGreenMessage("Initial twin desired properties: " + deviceTwin.Properties.Desired.ToJson());

            Thread.Sleep(4000);

            ConsoleHelper.WriteGreenMessage("STARTING...");

            // Set the device twin update callback.
            deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

            SendDeviceToCloudMessagesAsync();
            Console.ReadLine();
        }

        // Async method to send simulated telemetry.
        private static async void SendDeviceToCloudMessagesAsync()
        {
            while (true)
            {
                var currentTemperature = cheeseCave.ReadTemperature();
                var currentHumidity = cheeseCave.ReadHumidity();

                // Create JSON message.
                var telemetryDataPoint = new
                {
                    temperature = Math.Round(currentTemperature, 2, MidpointRounding.AwayFromZero),
                    humidity = Math.Round(currentHumidity, 2, MidpointRounding.AwayFromZero)
                };
                var messageString = JsonConvert.SerializeObject(telemetryDataPoint);

                ConsoleHelper.WriteGreenMessage($"Message data: {messageString} (Desired: temperature={cheeseCave.DesiredTemperature}+-{cheeseCave.desiredTempLimit}; humidity={cheeseCave.DesiredHumidity}+-{cheeseCave.desiredHumidityLimit})");

                //// Build the IoT Hub message to be sent.

                var message = new Message(Encoding.ASCII.GetBytes(messageString));

                // Add custom application properties to the message.
                message.Properties.Add("sensorID", "S1");

                // we always send the fanAlert as part of the message.
                var fanState = cheeseCave.FanState;
                message.Properties.Add("fanAlert", (fanState == StateEnum.Failed) ? "true" : "false");

                // Send temperature or humidity alerts, only if they occur.
                var isTemperatureAlert = cheeseCave.IsTemperatureAlert;
                if (isTemperatureAlert)
                {
                    message.Properties.Add("temperatureAlert", "true");
                }
                var isHumidityAlert = cheeseCave.IsHumidityAlert;

                if (isHumidityAlert)
                {
                    message.Properties.Add("humidityAlert", "true");
                }
                // There two alerts not sent optionally...

                ConsoleHelper.WriteGreenMessage($"     Properties: fanState={fanState} ; TemperatureAlert={isTemperatureAlert} ; HumidityAlert={isHumidityAlert}");

                //// Send the telemetry message.

                await deviceClient.SendEventAsync(message);
                ConsoleHelper.WriteGreenMessage("Message sent\n");

                await Task.Delay(intervalInMilliseconds);
            }
        }

        // INSERT SetFanState method below here
        // Handle the direct method call
        private static Task<MethodResponse> SetFanState(MethodRequest methodRequest, object userContext)
        {
            // Once failed, the Fan cannot be enabled again, using desired properties.
            if (cheeseCave.FanState == StateEnum.Failed)
            {
                // Acknowledge the direct method call with a 400 error message.
                string result = "{\"result\":\"Fan failed\"}";
                ConsoleHelper.WriteRedMessage("Direct method failed: " + result);
                return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
            }
            else
            {
                try
                {
                    var data = Encoding.UTF8.GetString(methodRequest.Data);

                    // Remove quotes from data.
                    data = data.Replace("\"", "");

                    // Parse the payload, and trigger an exception if it's not valid. 
                    // Expected values: Off, On, Failed
                    cheeseCave.UpdateFan((StateEnum)Enum.Parse(typeof(StateEnum), data));
                    ConsoleHelper.WriteGreenMessage("Fan set to: " + data);

                    // Acknowledge the direct method call with a 200 success message.
                    string result = "{\"result\":\"Executed direct method: " + methodRequest.Name + "\"}";
                    return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 200));
                }
                catch
                {
                    // Acknowledge the direct method call with a 400 error message.
                    string result = "{\"result\":\"Invalid parameter\"}";
                    ConsoleHelper.WriteRedMessage("Direct method failed: " + result);
                    return Task.FromResult(new MethodResponse(Encoding.UTF8.GetBytes(result), 400));
                }
            }
        }

        // INSERT OnDesiredPropertyChanged method below here
        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                // Update the Cheese Cave Simulator properties
                cheeseCave.DesiredHumidity = desiredProperties["humidity"];
                cheeseCave.DesiredTemperature = desiredProperties["temperature"];
                ConsoleHelper.WriteGreenMessage("Setting desired humidity to " + desiredProperties["humidity"]);
                ConsoleHelper.WriteGreenMessage("Setting desired temperature to " + desiredProperties["temperature"]);

                // Report the properties back to the IoT Hub.
                var reportedProperties = new TwinCollection();
                reportedProperties["fanstate"] = cheeseCave.FanState.ToString();
                reportedProperties["humidity"] = cheeseCave.DesiredHumidity;
                reportedProperties["temperature"] = cheeseCave.DesiredTemperature;
                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                ConsoleHelper.WriteGreenMessage("\nTwin state reported: " + reportedProperties.ToJson());
            }
            catch
            {
                ConsoleHelper.WriteRedMessage("Failed to update device twin");
            }
        }

    }

    internal class CheeseCaveSimulator
    {
        internal enum StateEnum
        {
            Off,
            On,
            Failed
        }

        // Global constants.
        private const double ambientTemperature = 70;               // Ambient temperature of a southern cave, in degrees F.
        private const double ambientHumidity = 99;                  // Ambient humidity in relative percentage of air saturation.
        internal double desiredTempLimit  { get; private set; } = 5;                  // Acceptable range above or below the desired temp, in degrees F.
        internal double desiredHumidityLimit  { get; private set; } = 10;             // Acceptable range above or below the desired humidity, in percentages.

        // state variables
        private double currentTemperature = ambientTemperature;     // initial value is set to the ambient value
        private double currentHumidity = ambientHumidity;           // initial value is set to the ambient value

        Random rand = new Random();

        internal StateEnum FanState { get; private set; } = StateEnum.Off;

        internal bool IsTemperatureAlert => (currentTemperature > DesiredTemperature + desiredTempLimit) || (currentTemperature < DesiredTemperature - desiredTempLimit);

        internal bool IsHumidityAlert => (currentHumidity > DesiredHumidity + desiredHumidityLimit) || (currentHumidity < DesiredHumidity - desiredHumidityLimit);

        public double DesiredTemperature { get; set; } = ambientTemperature - 10; // Initial desired temperature, in degrees F.

        public double DesiredHumidity { get; set; } = ambientHumidity - 20; // Initial desired humidity in relative percentage of air saturation.

        public double ReadTemperature()
        {
            // Simulate telemetry.
            double deltaTemperature = Math.Sign(DesiredTemperature - currentTemperature);

            if (FanState == StateEnum.On)
            {
                // If the fan is on the temperature and humidity will be nudged towards the desired values most of the time.
                currentTemperature += (deltaTemperature * rand.NextDouble()) + rand.NextDouble() - 0.5;

                // Randomly fail the fan.
                if (rand.NextDouble() < 0.01)
                {
                    FanState = StateEnum.Failed;
                    ConsoleHelper.WriteRedMessage("Fan has failed");
                }
            }
            else
            {
                // If the fan is off, or has failed, the temperature and humidity will creep up until they reaches ambient values, thereafter fluctuate randomly.
                if (currentTemperature < ambientTemperature - 1)
                {
                    currentTemperature += rand.NextDouble() / 10;
                }
                else
                {
                    currentTemperature += rand.NextDouble() - 0.5;
                }
            }

            return currentTemperature;
        }

        public double ReadHumidity()
        {
            // Simulate telemetry.
            double deltaHumidity = Math.Sign(DesiredHumidity - currentHumidity);

            if (FanState == StateEnum.On)
            {
                // If the fan is on the temperature and humidity will be nudged towards the desired values most of the time.
                currentHumidity += (deltaHumidity * rand.NextDouble()) + rand.NextDouble() - 0.5;

                // Randomly fail the fan.
                if (rand.NextDouble() < 0.01)
                {
                    FanState = StateEnum.Failed;
                    ConsoleHelper.WriteRedMessage("Fan has failed");
                }
            }
            else
            {
                // If the fan is off, or has failed, the temperature and humidity will creep up until they reaches ambient values, thereafter fluctuate randomly.
                if (currentHumidity < ambientHumidity - 1)
                {
                    currentHumidity += rand.NextDouble() / 10;
                }
                else
                {
                    currentHumidity += rand.NextDouble() - 0.5;
                }
            }

            // Check: humidity can never exceed 100%.
            currentHumidity = Math.Min(100, currentHumidity);

            return currentHumidity;
        }

        internal void UpdateFan(StateEnum newState)
        {
            // in a real device, this method would contain logic to start/stop
            // a fan and determine whether it was successful

            FanState = newState;
        }
    }

    internal static class ConsoleHelper
    {
        internal static void WriteColorMessage(string text, ConsoleColor clr)
        {
            Console.ForegroundColor = clr;
            Console.WriteLine(text);
            Console.ResetColor();
        }
        internal static void WriteGreenMessage(string text)
        {
            WriteColorMessage(text, ConsoleColor.Green);
        }

        internal static void WriteRedMessage(string text)
        {
            WriteColorMessage(text, ConsoleColor.Red);
        }
    }
}