using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace AzureDigitalTwinsPatchConverter
{
    public class PatchConverter
    {
        public static Patch GetPatch(string json)
        {
            JObject body = (JObject)JsonConvert.DeserializeObject(json);

            var patch = new Patch();

            patch.modelId = body["modelId"].Value<string>();
            patch.eventProcessedUtcTime = Convert.ToDateTime(body["EventProcessedUtcTime"]);
            patch.eventEnqueuedUtcTime = Convert.ToDateTime(body["EventEnqueuedUtcTime"]);
            patch.partitionId = Convert.ToInt32(body["PartitionId"]);

            patch.PatchItems = body["patch"].ToObject<List<PatchItem>>().ToArray();

            return patch;
        }
    }

    public class Patch
    {
        public string modelId { get; set; }

        public DateTime eventProcessedUtcTime { get; set; }

        public DateTime eventEnqueuedUtcTime { get; set; }

        public int partitionId { get; set; }

        public PatchItem[] PatchItems { get; set; }
    }

    public class PatchItem
    {
        public object value { get; set; }
        public string path { get; set; }
        public string op { get; set; }
    }
}
