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
using AzureDigitalTwinsPatchConverter;

namespace Contoso.AdtFunctions
{
    public static class PropFuncToGraphFunction
    {
        //Your Digital Twins URL is stored in an application setting in Azure Functions.
        private static readonly string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        // The code also follows a best practice of using a single, static,
        // instance of the HttpClient
        private static readonly HttpClient httpClient = new HttpClient();

        // USES CONSUMERGROUP 'graph'
        [FunctionName("PropFuncToGraphFunction")]
        public static async Task Run(
            [EventHubTrigger("evh-az220-adtprops2func", ConsumerGroup = "graph", Connection = "ADT_HUB_PROP_CONNECTIONSTRING")] EventData[] events,
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
                log.LogInformation("***************");
                foreach (var p in eventData.Properties)
                {
                    log.LogInformation($"{p.Key} - {p.Value}");
                }
                log.LogInformation("***************");

                var cloudEventsType = "Microsoft.DigitalTwins.Twin.Update";
                var cloudEventsDataSchema = "dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;2";

                if ((string)eventData.Properties["cloudEvents:type"] != cloudEventsType)
                {
                    log.LogWarning($"This function only supports cloudEvents type '{cloudEventsType}'");

                    await Task.Yield();
                    continue;
                }

                try
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
                            string twinId = eventData.Properties["cloudEvents:subject"].ToString();

                            string messageBody =
                                Encoding.UTF8.GetString(
                                    eventData.Body.Array,
                                    eventData.Body.Offset,
                                    eventData.Body.Count);

                            log.LogInformation($"'{twinId}' Body received: {messageBody}");

                            var convertedPatch = PatchConverter.GetPatch(messageBody);

                            if (convertedPatch.modelId != cloudEventsDataSchema)
                            {
                                log.LogWarning($"This function only supports cloudevents dataschema '{cloudEventsDataSchema}'");

                                await Task.Yield();
                                continue;
                            }

                            //Find and update parent Twin
                            string parentId = await AdtUtilities.FindParentAsync(client, twinId, "rel_has_devices", log);

                            if (parentId != null)
                            {
                                log.LogInformation($"PARENT {parentId} FOUND");

                                var patch = new Azure.JsonPatchDocument();

                                patch.AppendReplace("/fanAlert", Convert.ToBoolean( convertedPatch.PatchItems.First(x => x.path == "/fanAlert").value));
                                patch.AppendReplace("/temperatureAlert", Convert.ToBoolean(convertedPatch.PatchItems.First(x => x.path == "/temperatureAlert").value));
                                patch.AppendReplace("/humidityAlert", Convert.ToBoolean(convertedPatch.PatchItems.First(x => x.path == "/humidityAlert").value));

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
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
