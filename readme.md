# AZ 220 training for Azure Digital Twins - Extended

## Introduction

This device simulation is part of the AZ220 Digital twins training as seen at this learning path:

  https://docs.microsoft.com/en-us/learn/paths/extend-iot-solutions-by-using-azure-digital-twins/

There, you will be introduced into extending your Azure IoT solution with Azure Digital Twins.

This is part of the AZ 220 Azure IoT Developer exam available at:

  https://docs.microsoft.com/en-us/certifications/exams/az-220

The original, complete, training lab can be found here:

  https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/tree/master/Allfiles/Labs/19-Azure%20Digital%20Twins

The original instructions of this lab are available here:

  https://github.com/MicrosoftLearning/AZ-220-Microsoft-Azure-IoT-Developer/blob/master/Instructions/Labs/LAB_AK_19-azure-digital-twins.md 

Below we will be introduced to a number of additial features which turn the training material into a more complete solution.

## Original Training material, entended

The training material provided is a decent starting point for getting familiar with Azure Digital Twins.

After you have worked through the workshop, you will have created:

- A live Azure Digital Twins environment around a cheese factory having three caves. Each cave has a temperature and humidity sensor
- For cave 1, sensor 55 will be sending telemetry to an IoT Hub
- An Azure Function picks up the device telemetry and sends it to the digital twin representation, both as property patches (for alert properties) and as telemetry (temperature and humidity)
- Device twin telemetry events are routed, outputted to an eventhub endpoint using internal event routes 
- Events from that event route are picked up by another Azure Function and enriched so these can be picked up by Time Series Insights 

Although this is a great start, it's still just a start...

With a little more effort, this ADT solution could be a great example and demonstration more exciting ADT capabilities.

What the simulation is missing:
- More elaborate device simulation, supporting multiple devices
- Visual feedback of incoming telemetry?
- Device telemetry is not updating cave temperature and humidity, propagate Azure Digital Twins events through the graph
- Device properties are not updating cave alerts, propagate Azure Digital Twins properties through the graph
- Visualisaton of the ADT model in a 3D environment

## Additions, step-by-step

Here are the additions to the training so you can build a more appropiate ADT solution with 3D visualization.

### More elaborate device simulation, supporting multiple devices

For the Device simulation, minor changes to the code are made.

Some visual changes have been made but the original telemetry messages are still sent and the same logic (device interface) is used.

Just provide a connectionstring (see the readme in the project for right 'appsettings.json' file format) and fire up the console app.

### Visual feedback of incoming telemetry?

At the end of excercise 8 of the ADT training, is says:

  "You should be able to see that the fanAlert, temperatureAlert and humidityAlert properties have been updated."

Well, because the simulation device is quite slow in reaching points where alert values are changing it's not clear if the telemetry is actually arriving in the model.

You can check the Azure Function logging output but we want to see proof in the Model graph.

A smarter way to see if the sensor-th-55 twin is updated, is by looking at the metadata. 

All properties have a lastUpdateTime. Refresh the graph a number of times .You should see the eg. fanAlert lastUpdateTime changing over time.

I also experimented with extending the device model to show temperature and humidity as properties.

Still, updating the already excisting parent cave temperature and humidity is the most elegant way.

In the end, that extended device was replace by updating the cave but you are free to play with this extension.

Check out the readme in the DTDL models section for more details.

### Device telemetry is not updating cave temperature and humidity, propagate Azure Digital Twins events through the graph

From a Digital Twins point of view, we are modelling the real world so we are not interested in device telemetry, we want to know the temperature in the caves.

So, updating the cave properties, based on the child device telemetry is the right way to do this.

There are already some example on how to do this using the ADT documentation:

https://docs.microsoft.com/en-us/azure/digital-twins/tutorial-end-to-end#propagate-azure-digital-twins-events-through-the-graph

Here is an actual code example: 
https://github.com/Azure-Samples/digital-twins-samples/blob/main/AdtSampleApp/SampleFunctionsApp/ProcessDTRoutedData.cs

Based on this, I integrated a new Azure function, listening for device telemetry and updating parent caves.

#### Propagate Azure Digital Twins patch updates through the graph

To give an insight in the patch messages, look at this one:

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

I created a simple patch message converter so the JSON messages are more accessible in C#.

### Device properties are not updating cave alerts, propagate Azure Digital Twins properties through the graph

The sames goes for device properties, the device alerts should be propagated through the hierachy to the parent cave.

Because this is a separate ADT event, I routed these device model twin updates to another Azure Function.

This is because the Digital twin update message event format differs form the format of a telemetry event. 

### Visualisaton of the ADT model in a 3D environment

Last but not least, the ADT graph builder tooling is great for building but it's not something to share with customers.

Using Azure functions in conjunction with the ADT event routing, makes it possible to build a custom 'head' on top of the ADT ernvironment, up to a visualization using the Hololens.

Luckely, the Azure Device Twins environment offers a basic but still powerfull 3D representation too.

https://docs.microsoft.com/en-us/azure/digital-twins/quickstart-3d-scenes-studio 
https://docs.microsoft.com/en-us/azure/digital-twins/how-to-use-3d-scenes-studio

Based on the a 3D model created in Paint3D, I was able to upload it (you have to provide a separate Storage account blob container for this) and use it as representation.

#### Model

The model is created with Paint3D in Windows 11. 

It is not viewable in the Windows 10 3D Viewer.

## Nice to haves

Some nice-to-haves are:
- Desired values in the model digital twins are not related to the iot hub device twins
- Time Series Insights is becoming deprecated in 2025. It's time to move on to ADX.

It's up to you to contribute to this project. Pull requests are accepted.