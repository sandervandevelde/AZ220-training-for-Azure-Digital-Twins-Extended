using Azure;
using Azure.DigitalTwins.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Contoso.AdtFunctions
{
    /// <summary>
    /// Taken from https://github.com/Azure-Samples/digital-twins-samples/blob/main/AdtSampleApp/SampleFunctionsApp/AdtUtilities.cs
    /// </summary>
    internal static class AdtUtilities
    {
        public static async Task<string> FindParentAsync(DigitalTwinsClient client, string childId, string relname, ILogger log)
        {
            var childIdparts = childId.Split('/');

            var shortChildId = childIdparts.Length > 1 ? childIdparts[childIdparts.Length - 1] : childId;

            // Find parent using incoming relationships
            try
            {
                AsyncPageable<IncomingRelationship> rels = client.GetIncomingRelationshipsAsync(shortChildId);

                await foreach (IncomingRelationship ie in rels)
                {
                    if (ie.RelationshipName == relname)
                        return (ie.SourceId);
                }
            }
            catch (RequestFailedException exc)
            {
                log.LogInformation($"*** Error in retrieving parent:{exc.Status}:{exc.Message}");
            }
            return null;
        }
    }
}