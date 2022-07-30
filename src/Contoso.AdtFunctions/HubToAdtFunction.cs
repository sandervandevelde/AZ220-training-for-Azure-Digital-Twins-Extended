// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System.Net.Http;
using Azure.Core.Pipeline;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contoso.AdtFunctions
{
    public static class HubToAdtFunction
    {
        //Your Digital Twins URL is stored in an application setting in Azure Functions.
        private static readonly string adtInstanceUrl =
            Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        // The code also follows a best practice of using a single, static,
        // instance of the HttpClient
        private static readonly HttpClient httpClient = new HttpClient();

        // Notice the use of the FunctionName attribute to mark the Run
        // method as the entry point Run for HubToAdtFunction. The
        // method is also declared `async` as the code to update the Azure
        // Digital Twin runs asynchronously.
        // The eventGridEvent parameter is assigned the Event Grid event
        // that triggered the function call and the log parameter provides
        // access to a logger that can be used for debugging.
        [FunctionName("HubToAdtFunction")]
        public async static Task Run(
            [EventGridTrigger] EventGridEvent eventGridEvent,
            ILogger log)
        {
            // The ILogger interface is defined in the
            // Microsoft.Extensions.Logging namespace and aggregates most
            // logging patterns to a single method call. In this case, a log
            // entry is created at the Information level - other methods
            // exists for various levels including critical, error. etc. As the
            // Azure Function is running in the cloud, logging is essential
            // during development and production.
            log.LogInformation(eventGridEvent.Data.ToString());

            // This code checks if the adtInstanceUrl environment variable
            // has been set - if not, the error is logged and the function exits.
            // This demonstrates the value of logging to capture the fact that
            // the function has been incorrectly configured.
            if (adtInstanceUrl == null)
            {
                log.LogError("Application setting \"ADT_SERVICE_URL\" not set");
                return;
            }

            try
            {
                // REVIEW authentication code below here
                // Notice the use of the ManagedIdentityCredential class.
                // This class attempts authentication using the managed identity
                // that has been assigned to the deployment environment earlier.
                // Once the credential is returned, it is used to construct an
                // instance of the DigitalTwinsClient. The client contains
                // methods to retrieve and update digital twin information, like
                // models, components, properties and relationships.
                ManagedIdentityCredential cred =
                    new ManagedIdentityCredential("https://digitaltwins.azure.net");
                DigitalTwinsClient client =
                    new DigitalTwinsClient(
                        new Uri(adtInstanceUrl),
                        cred,
                        new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation($"Azure digital twins service client connection created.");

                // REVIEW event processing code below here
                if (eventGridEvent != null && eventGridEvent.Data != null)
                {

                    // Read deviceId and temperature for IoT Hub JSON.
                    // Notice the use of JSON deserialization to access the event data.
                    // The message properties and systemProperties are
                    // easily accessible using an indexer approach.
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                    var fanAlert = (bool)deviceMessage["properties"]["fanAlert"]; // cast directly to a bool

                    // However where properties are optional, such as temperatureAlert
                    // and humidityAlert, the use of `SelectToken` and a
                    // null-coalescing operation is required to prevent an
                    // exception being thrown.
                    var temperatureAlert = deviceMessage["properties"].SelectToken("temperatureAlert") ?? false; // JToken object
                    var humidityAlert = deviceMessage["properties"].SelectToken("humidityAlert") ?? false; // JToken object
                    log.LogInformation($"Device:{deviceId} fanAlert is:{fanAlert}");
                    log.LogInformation($"Device:{deviceId} temperatureAlert is:{temperatureAlert}");
                    log.LogInformation($"Device:{deviceId} humidityAlert is:{humidityAlert}");

                    // The message body contains the telemetry payload and is
                    // ASCII encoded JSON. Therefore, it must first be decoded and
                    // then deserialized before the telemetry properties can be accessed.
                    var bodyJson = Encoding.ASCII.GetString((byte[])deviceMessage["body"]);
                    JObject body = (JObject)JsonConvert.DeserializeObject(bodyJson);
                    var temperature = body["temperature"].Value<double>();
                    var humidity = body["humidity"].Value<double>();
                    log.LogInformation($"Device:{deviceId} Temperature is:{temperature}");
                    log.LogInformation($"Device:{deviceId} Humidity is:{humidity}");

                    // REVIEW ADT update code below here
                    // There are two approaches being used to apply data to the
                    // digital twin - the first via property updates using a JSON
                    // patch, the second via the publishing of telemetry data.
                    // The ADT client utilizes a JSON Patch document to add or
                    // update digital twin properties. The JSON Patch defines a
                    // JSON document structure for expressing a sequence of
                    // operations to apply to a JSON document. The various values
                    // are added to the patch as append or replace operations,
                    // and the ADT is then updated asynchronously.
                    var patch = new Azure.JsonPatchDocument();
                    patch.AppendReplace<bool>("/fanAlert", fanAlert); // already a bool
                    patch.AppendReplace<bool>("/temperatureAlert", temperatureAlert.Value<bool>()); // convert the JToken value to bool
                    patch.AppendReplace<bool>("/humidityAlert", humidityAlert.Value<bool>()); // convert the JToken value to bool

                    patch.AppendReplace<double>("/temperature", temperature); // convert the JToken value to bool
                    patch.AppendReplace<double>("/humidity", humidity); // convert the JToken value to bool

                    try
                    {
                        log.LogInformation($"PATCHING: {patch.ToString()}");                        
                    }
                    catch (System.Exception ex)
                    {
                        log.LogError($"patch unavailable: {ex.Message}");
                    }

                    await client.UpdateDigitalTwinAsync(deviceId, patch);

                    // publish telemetry
                    // Notice that the telemetry data is handled differently than
                    // the properties - rather than being used to set digital twin
                    // properties, it is instead being published as telemetry
                    // events. This mechanism ensures that the telemetry is
                    // available to be consumed by any downstream subscribers to
                    // the digital twins event route.
                    await client.PublishTelemetryAsync(deviceId, null, bodyJson);
                }
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }
        }
    }
}