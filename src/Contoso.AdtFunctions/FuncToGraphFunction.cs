using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System.Net.Http;
using Azure.Core.Pipeline;


namespace Contoso.AdtFunctions
{
    public static class FuncToGraphFunction
    {
        //Your Digital Twins URL is stored in an application setting in Azure Functions.
        private static readonly string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        // The code also follows a best practice of using a single, static,
        // instance of the HttpClient
        private static readonly HttpClient httpClient = new HttpClient();


        // USES CONSUMERGROUP 'graph'
        [FunctionName("FuncToGraphFunction")]
        public static async Task Run(
            [EventHubTrigger("evh-az220-adt2func", ConsumerGroup = "graph", Connection = "ADT_HUB_CONNECTIONSTRING")] EventData[] events,
            ILogger log)
        {
            log.LogInformation($"Executing: {events.Length} events...");

            if (adtServiceUrl == null)
            {
                log.LogError("Application setting \"ADT_SERVICE_URL\" not set");
                return;
            }

            // As this function is processing a batch of events, a way to handle
            // errors is to create a collection to hold exceptions. The function
            // will then iterate through each event in the batch, catching
            // exceptions and adding them to the collection. At the end of the
            // function, if there are multiple exceptions, an AggregateException
            // is created with the collection, if a single exception is generated,
            // then the single exception is thrown.
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    // REVIEW check telemetry below here
                    // This code checks that the current event is telemetry from
                    // a Cheese Cave Device ADT twin - if not, logs that it isn't
                    // and then forces the method to complete asynchronously -
                    // this can make better use of resources.
                    if ((string)eventData.Properties["cloudEvents:type"] == "microsoft.iot.telemetry"
                            && (string)eventData.Properties["cloudEvents:dataschema"] == "dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;2")
                    {
                        DigitalTwinsClient client;
                        // Authenticate on ADT APIs
                        try
                        {
                            ManagedIdentityCredential cred =
                                new ManagedIdentityCredential("https://digitaltwins.azure.net");

                            client = new DigitalTwinsClient(
                                            new Uri(adtServiceUrl),
                                            cred,
                                            new DigitalTwinsClientOptions
                                            {
                                                Transport = new HttpClientTransport(httpClient)
                                            });

                            log.LogInformation("ADT service client connection created.");

                            if (client != null)
                            {
                                log.LogInformation("***************");
                                foreach(var p in eventData.Properties)
                                {
                                    log.LogInformation($"{p.Key} - {p.Value}");
                                }
                                log.LogInformation("***************");

                                string twinId = eventData.Properties["cloudEvents:source"].ToString();

                                // REVIEW TSI Event creation below here
                                // The event is Cheese Cave Device Telemetry
                                // As the eventData.Body is defined as an ArraySegment,
                                // rather than just an array, the portion of the underlying
                                // array that contains the messageBody must be extracted,
                                // and then deserialized.
                                string messageBody =
                                    Encoding.UTF8.GetString(
                                        eventData.Body.Array,
                                        eventData.Body.Offset,
                                        eventData.Body.Count);
                                JObject body = (JObject)JsonConvert.DeserializeObject(messageBody);

                                log.LogInformation($"Body received: {messageBody}");

                                //Find and update parent Twin
                                string parentId = await AdtUtilities.FindParentAsync(client, twinId, "rel_has_devices", log);

//                                string parentId = await AdtUtilities.FindParentByQueryAsync(client, twinId, "rel_has_devices", log);

                                if (parentId != null)
                                {
                                    log.LogInformation($"PARENT {parentId} FOUND");

                                    var temperature = body["temperature"].Value<double>();
                                    var humidity = body["humidity"].Value<double>();

                                    var patch = new Azure.JsonPatchDocument();
                                    //patch.AppendReplace<bool>("/fanAlert", fanAlert); // already a bool
                                    //patch.AppendReplace<bool>("/temperatureAlert", temperatureAlert.Value<bool>()); // convert the JToken value to bool
                                    //patch.AppendReplace<bool>("/humidityAlert", humidityAlert.Value<bool>()); // convert the JToken value to bool

                                    patch.AppendReplace<double>("/temperature", temperature); // convert the JToken value to bool
                                    patch.AppendReplace<double>("/humidity", humidity); // convert the JToken value to bool

                                    try
                                    {
                                        log.LogInformation($"PATCHING: {patch}");
                                    }
                                    catch (System.Exception ex)
                                    {
                                        log.LogError($"patch unavailable: {ex.Message}");
                                    }

                                    // deviceid IS the twinid in this case!
                                    await client.UpdateDigitalTwinAsync(parentId, patch);
                                }
                                else
                                {
                                    log.LogInformation($"NO PARENT FOUND");
                                }
                            }       
                        }
                        catch (Exception e)
                        {
                            log.LogError($"ADT service client connection failed. {e}");
                            return;
                        }

                        // // A Dictionary is then instantiated to hold the
                        // // key/value pairs that will make up the properties sent
                        // // within the TSI event.
                        // // Notice that the cloudEvents:source property
                        // // (which contains the fully qualified twin ID - similar
                        // // to `adt-az220-training-dm030821.api.eus.digitaltwins.azure.net/digitaltwins/sensor-th-0055`)
                        // // is assigned to the \$dtId key. This key has special
                        // // meaning as the Time Series Insights environment created
                        // // during setup is using \$dtId as the Time Series
                        // // ID Property.
                        // var tsiUpdate = new Dictionary<string, object>();
                        // tsiUpdate.Add("$dtId", eventData.Properties["cloudEvents:source"]);
                        // // The temperature and humidity values are extracted
                        // // from the message and added to the TSI update.
                        // tsiUpdate.Add("temperature", message["temperature"]);
                        // tsiUpdate.Add("humidity", message["humidity"]);

                        // // The update is then serialized to JSON and added to the
                        // // outputEvents which publishes the update to the Event Hub
                        // var tsiUpdateMessage = JsonConvert.SerializeObject(tsiUpdate);
                        // log.LogInformation($"TSI event: {tsiUpdateMessage}");

                        // await outputEvents.AddAsync(tsiUpdateMessage);
                    }
                    else
                    {
                        log.LogInformation($"Not Cheese Cave Device telemetry version 2");
                        await Task.Yield();
                    }
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture
                    // this exception and continue.
                    // Also, consider capturing details of the message that failed
                    // processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the
            // batch failed processing throw an exception so that there is a
            // record of the failure.
            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
