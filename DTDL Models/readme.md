# device model extended

I experimented with extending the device model to show temperature and humidity as properties.

Still, updating the already excisting parent cave temperature and humidity is the most elegant way.

But you are free to play with this solution.

#### Changing the model, entending the device

In the device model, I added two extra properties:

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

*Note*: So you need to delete all current (device) twins and upload new once using the XLSX (although you need to fix it for the new version 2). This update can take a while. 

*Note*: Because we update the modelID, the function listening to messages to be sent to TSI, has to change too (it listens to specific model messages; see the code); 

I also updated the ingestion Azure Function to fill these two properties:

```
var bodyJson = Encoding.ASCII.GetString((byte[])deviceMessage["body"]);
JObject body = (JObject)JsonConvert.DeserializeObject(bodyJson);
var temperature = body["temperature"].Value<double>();
var humidity = body["humidity"].Value<double>();
...
patch.AppendReplace<double>("/temperature", temperature); // convert the JToken value to bool
patch.AppendReplace<double>("/humidity", humidity); // convert the JToken value to bool
```

Opposite to telemetry, ADT twin properties are persited in the graph representation and therefor visible in the graph. 

Once this is set in place, you will see the right temperature values shown when you refresh the graph (skip around between devices to see updated values).

## Device connection

Check the properties/metadata to see if the devices support temperature and humidity properties already.

If not, you will see this error in the Azure function logging when it tries to patch a device:

```
2022-07-30T20:22:55.285 [Error] Service request failed.
Status: 400 (Bad Request)

Content:
{"error":{"code":"JsonPatchInvalid","message":"humidity does not exist on component. Please provide a valid patch document. See section on update apis in the documentation https://aka.ms/adtv2twins."}}
```

First check the support for the properties in the other device twins again. This can take some time.

Then, when the right connection string is used for the simulation, you will see the telemetry being ingested for the other two devices.