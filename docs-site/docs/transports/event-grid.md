---
sidebar_position: 17
title: Azure Event Grid
description: Azure Event Grid transport — publish-only egress to an Event Grid topic, with CloudEvents or Event Grid schema, key or managed-identity authentication.
---

# Azure Event Grid Transport

`Excalibur.Dispatch.Transport.AzureServiceBus` registers an **Azure Event Grid** publisher that sends dispatch messages to an [Event Grid](https://learn.microsoft.com/azure/event-grid/overview) topic.

:::info Publish-only
Event Grid is an **egress** transport here: it publishes to a topic and does not subscribe. Event Grid delivers to subscribers it owns (webhooks, queues, functions), so there is no receiver to register on this side. If you need to consume what Event Grid delivers, point an Event Grid subscription at a destination you already consume — an Azure Service Bus queue, for example — and use that transport to receive.
:::

## Before You Start

- **.NET 10.0**
- An Event Grid topic, and either its access key or a managed identity with permission to publish to it
- Familiarity with [transports](./index.md)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.AzureServiceBus
```

Event Grid ships inside the Azure Service Bus transport package; there is no separate package to install.

## Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddEventGridTransport(options =>
{
    options.TopicEndpoint = "https://mytopic.westus2-1.eventgrid.azure.net/api/events";
    options.AccessKey = configuration["EventGrid:AccessKey"];
});
```

Or bind from configuration:

```csharp
services.AddEventGridTransport(configuration.GetSection("EventGrid"));
```

Both overloads validate on start, so a misconfigured topic fails at host startup rather than on the first publish.

## Options

`EventGridTransportOptions`:

| Option | Default | Notes |
|--------|---------|-------|
| `TopicEndpoint` | *(none — required)* | The topic endpoint URI, for example `https://mytopic.westus2-1.eventgrid.azure.net/api/events`. Startup fails without it. |
| `AccessKey` | `null` | The topic access key. **Leave it null to use managed identity** (`DefaultAzureCredential`). |
| `SchemaMode` | `CloudEvents` | `CloudEvents` or `EventGridSchema`. Must match the schema the topic was created with. |
| `Destination` | `"eventgrid-default"` | The logical destination name used for routing. |
| `DefaultEventType` | `"Excalibur.Dispatch.TransportMessage"` | The `type` on published events when a message does not specify one. |
| `DefaultEventSource` | `"/excalibur/dispatch"` | The `source` on published events. |

### TopicEndpoint is required

`TopicEndpoint` has no usable default, so the options validator rejects an empty one at startup with:

> `TopicEndpoint is required. Set EventGridTransportOptions.TopicEndpoint to the Event Grid topic endpoint URI.`

Copy the endpoint from the topic's **Overview** blade in the Azure portal, including the `/api/events` path.

### Authentication: key or managed identity

Setting `AccessKey` authenticates with that key. **Leaving it `null` switches to `DefaultAzureCredential`**, which resolves a managed identity in Azure and your developer credentials locally — the option's absence is the switch, so a null key is a deliberate configuration rather than a missing one.

Prefer managed identity where the host supports it, and keep the key out of source when you use one:

```csharp
services.AddEventGridTransport(options =>
{
    options.TopicEndpoint = configuration["EventGrid:TopicEndpoint"]!;
    // AccessKey left null -> DefaultAzureCredential
});
```

### SchemaMode must match the topic

An Event Grid topic is created with one schema and does not accept the other. `CloudEvents` is the default here and the one to prefer for new topics; choose `EventGridSchema` only for a topic already created with the Event Grid schema. A mismatch is rejected by Event Grid at publish time, not at startup.

## What's Next

- [Transports overview](./index.md) — the transport seam and what a transport provides
- [Choosing a transport](./choosing-a-transport.md)
- [Azure Service Bus](./azure-service-bus.md) — a receive-capable Azure transport, and a common Event Grid subscription destination
