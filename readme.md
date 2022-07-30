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
- Clear visual feedback of incoming telemetry
- Device telemetry is not updating cave temperatures
- No visual representation in a 3D environment
- Supporting 3 sensors instead of 1 sensor

Some nice to haves are:
- Desired values in the model digital twins are not related to the iot hub device twins
- Time Series Insights is becoming deprecated in 2025. It's time to move on to ADX.



