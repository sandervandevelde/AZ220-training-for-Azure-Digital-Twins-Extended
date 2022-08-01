# AZ 220 training

## Introduction

This device simulation is part of the AZ220 Digital twins training as seen at this learning path:

  https://docs.microsoft.com/en-us/learn/paths/extend-iot-solutions-by-using-azure-digital-twins/

There, you will be introduced into Azure Digital Twins.

This is part of the AZ 220 Azure IoT Developer exam available at:

  https://docs.microsoft.com/en-us/certifications/exams/az-220

The complete training lab can be found here:

  https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/19-Azure%20Digital%20Twins

The instructions of this lab are available here:

  https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/blob/master/Instructions/Labs/LAB_AK_19-azure-digital-twins.md 

Below we will be introduced to a number of additial features which turn the training material in a more complete solution.

## Device simulation, changes to the code

Regarding the device simulation, some visual changes have been made but the same messages are sent and the same logic is used.

Just provide a connectionstring (see readme for right appsettings file format) and fire up the console app.

## Original Training material

The training material provided is a decent starting point for getting familiar with Azure Digital Twins.

After you have worked through the workshop, you will have created:

- A live Azure Digital Twins environment around a cheese factory having three caves. Each cave has a temperature and humidity sensor
- For cave 1, a sensor is sending telemetry to an IoT Hub
- An Azure Function picks up the device telemetry and sends it to the digital twin representation, both as property patches (for alerts) and telemetry (temperature and humidity)
- Device twin telemetry events are outputted to an eventhub using internal event routes 
- Event from that event route are picked up by another Azure Function and enriched so it can be picked up by Time Series Insights 

Although this is a great start, it's just a start...

With a little more effort, this ADT solution could be a great example and demonstration of all ADT capabilities.

What the simulation is missing:
- Better visual feedback of incoming telemetry
- Device telemetry is not updating cave temperatures
- No visual representation in a 3D environment
- Supporting 3 sensors instead of 1 sensor

Some nice-to-haves are:
- Desired values in the model digital twins are not related to the iot hub device twins
- Time Series Insights is becoming deprecated in 2025. It's time to move on to ADX.

## Additions, step-by-step

Here are some additions to the training so you can build a more appropiate ADT solution with 3D visualization.

### Better visual feedback of incoming telemetry

At the end of excercise 8 of the ADT training, is says:

  "You should be able to see that the fanAlert, temperatureAlert and humidityAlert properties have been updated."

Well, because the simulation device is quite slow in reaching points where alert values are changing it's not clear if the telemetry is actually arriving in the model.

You can check the Azure Function logging output but we want to see proof in the Model graph.

One other way to see if the sensor-th-55 twin is updated, is by looking at the metadata. 

All properties have a lastUpdateTime. Refresh the graph a number of times .You should see the eg. fanAlert lastUpdateTime changing over time.

Still this is not the most elegant way.

#### Changing the model

In the DTDL models folder, I added a updated model for the device.

In this model, I added two extra properties:

```
{
  "@type": ["Property", "Temperature"],
  "name": "temperature",
  "schema": "double",
  "unit": "degreeFahrenheit",
  "description": "Last ingested temperature"
},
{
  "@type": "Property",
  "name": "humidity",
  "schema": "double",
  "description": "Last ingested humidity"
}
```
I updated the version of the model:

  dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;2

*Note*: So you need to delete all current twins and upload new once usin the XLSX. Note, this update can take a while. 

*Note*: Because we update the modelID, the function listening to messages to be sent to TSI has to change too (it listens to specific model messages); 

We also updated the ingestion Azure Function to fill these two properties:

```
var bodyJson = Encoding.ASCII.GetString((byte[])deviceMessage["body"]);
JObject body = (JObject)JsonConvert.DeserializeObject(bodyJson);
var temperature = body["temperature"].Value<double>();
var humidity = body["humidity"].Value<double>();
...
patch.AppendReplace<double>("/temperature", temperature); // convert the JToken value to bool
patch.AppendReplace<double>("/humidity", humidity); // convert the JToken value to bool
```

ADT twin propertis are persited in the graph representation and visible. 

Once this is set in place, you will see the right temperature values shown when you refresh the graph.

At this moment it feel a bit redundant. The device both has a temperature property and telemetry. The same goes for the humidity. This is true actually. We will fix this in the next section.

### Support multiple devices

Supporting multiple devices is technically done already.

First, just register the other two devices (56 and 57) in the IoT Hub.

The device twins are pointing the right modelId already. 

Check the pproperties/metadata if both devices support temperature and humidity properties already.

If not, you will see this error in the Azure function logging when it tries to patch a device:

```
2022-07-30T20:22:55.285 [Error] Service request failed.
Status: 400 (Bad Request)

Content:
{"error":{"code":"JsonPatchInvalid","message":"humidity does not exist on component. Please provide a valid patch document. See section on update apis in the documentation https://aka.ms/adtv2twins."}}
```

First check the support for the properties in the other device twins again. This can take some time.

Then, when the right connection string is used for the simulation, you will see the telemetry being ingested for the other two devices.

### Propagate Azure Digital Twins events through the graph

https://docs.microsoft.com/en-us/azure/digital-twins/tutorial-end-to-end#propagate-azure-digital-twins-events-through-the-graph

code example: to be integrated:
https://github.com/Azure-Samples/digital-twins-samples/blob/main/AdtSampleApp/SampleFunctionsApp/ProcessDTRoutedData.cs


### Propagate Azure Digital Twins patch updates through the graph

```
{
  "modelId": "dtmi:com:contoso:digital_factory:cheese_factory:cheese_cave_device;2",
  "patch": [
    {
      "value": 0,
      "path": "/fanAlert",
      "op": "replace"
    },
    {
      "value": 1,
      "path": "/temperatureAlert",
      "op": "replace"
    },
    {
      "value": 1,
      "path": "/humidityAlert",
      "op": "replace"
    },
    {
      "value": 81.18,
      "path": "/temperature",
      "op": "replace"
    },
    {
      "value": 98.24,
      "path": "/humidity",
      "op": "replace"
    }
  ],
  "EventProcessedUtcTime": "2022-08-01T13:20:28.1939131Z",
  "PartitionId": 0,
  "EventEnqueuedUtcTime": "2022-08-01T13:18:22.3950000Z"
}
```


### Visualisaton in 3D

https://docs.microsoft.com/en-us/azure/digital-twins/quickstart-3d-scenes-studio 
https://docs.microsoft.com/en-us/azure/digital-twins/how-to-use-3d-scenes-studio


#### Model

The model is created with Paint3D in Windows 11. 

It is not viewable in the Windows 10 3D Viewer.