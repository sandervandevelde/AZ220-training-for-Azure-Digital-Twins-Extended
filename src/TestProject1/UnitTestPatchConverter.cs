using AzureDigitalTwinsPatchConverter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace TestProject1
{
    [TestClass]
    public class UnitTestPatchConverter
    {
        [TestMethod]
        public void TestMethodGetPatch_patch1()
        {
            //// ARRANGE
            var json = File.ReadAllText("patch1.json");

            //// ACT
            var patch = PatchConverter.GetPatch(json);

            //// ASSERT
            Assert.AreEqual("01/08/2022 13:20:28", patch.eventProcessedUtcTime.ToString());
            Assert.AreEqual("01/08/2022 13:18:22", patch.eventEnqueuedUtcTime.ToString());
            Assert.AreEqual(0, patch.partitionId);
            Assert.AreEqual("dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;1", patch.modelId);
            Assert.AreEqual(3, patch.PatchItems.Length);
        }


        [TestMethod]
        public void TestMethodGetPatch_patch2()
        {
            //// ARRANGE
            var json = File.ReadAllText("patch2.json");

            //// ACT
            var patch = PatchConverter.GetPatch(json);

            //// ASSERT
            Assert.AreEqual("dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;1", patch.modelId);
            Assert.AreEqual(3, patch.PatchItems.Length);
        }

    }
}
