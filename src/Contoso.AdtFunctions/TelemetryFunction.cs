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

namespace Consoto.AdtFunctions
{
    public static class TelemetryFunction
    {
        // Take a moment to look at the Run method definition. The events
        // parameter makes use of the EventHubTrigger attribute - the
        // attribute's constructor takes the name of the event hub, the optional
        // name of the consumer group ($Default is used if omitted), and the
        // name of an app setting that contains the connection string. This
        // configures the function trigger to respond to an event sent to an
        // event hub event stream. As events is defined as an array of EventData,
        // it can be populated with a batch of events.
        // The next parameter, outputEvents has the EventHub attribute -
        // the attribute's constructor takes the name of the event hub and the
        // name of an app setting that contains the connection string. Adding
        // data to the outputEvents variable will publish it to the associated
        // Event Hub.
        [FunctionName("TelemetryFunction")]
        public static async Task Run(
            [EventHubTrigger("evh-az220-adt2func", Connection = "ADT_HUB_CONNECTIONSTRING")] EventData[] events,
            [EventHub("evh-az220-func2tsi", Connection = "TSI_HUB_CONNECTIONSTRING")] IAsyncCollector<string> outputEvents,
            ILogger log)
        {
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
                    if ((string)eventData.Properties["cloudEvents:type"] == "microsoft.iot.telemetry" &&
                        (string)eventData.Properties["cloudEvents:dataschema"] == "dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;1")
                    {
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
                        JObject message = (JObject)JsonConvert.DeserializeObject(messageBody);

                        // A Dictionary is then instantiated to hold the
                        // key/value pairs that will make up the properties sent
                        // within the TSI event.
                        // Notice that the cloudEvents:source property
                        // (which contains the fully qualified twin ID - similar
                        // to `adt-az220-training-dm030821.api.eus.digitaltwins.azure.net/digitaltwins/sensor-th-0055`)
                        // is assigned to the \$dtId key. This key has special
                        // meaning as the Time Series Insights environment created
                        // during setup is using \$dtId as the Time Series
                        // ID Property.
                        var tsiUpdate = new Dictionary<string, object>();
                        tsiUpdate.Add("$dtId", eventData.Properties["cloudEvents:source"]);
                        // The temperature and humidity values are extracted
                        // from the message and added to the TSI update.
                        tsiUpdate.Add("temperature", message["temperature"]);
                        tsiUpdate.Add("humidity", message["humidity"]);

                        // The update is then serialized to JSON and added to the
                        // outputEvents which publishes the update to the Event Hub
                        var tsiUpdateMessage = JsonConvert.SerializeObject(tsiUpdate);
                        log.LogInformation($"TSI event: {tsiUpdateMessage}");

                        await outputEvents.AddAsync(tsiUpdateMessage);
                    }
                    else
                    {
                        log.LogInformation($"Not Cheese Cave Device telemetry");
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
