# Excalibur.Dispatch.Transport.Abstractions

Abstractions for message transport implementations in the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Transport.Abstractions
```

## Key Types

- `IMessageTransport` - Transport interface for sending/receiving messages
- `IMessageConsumer` - Consumer interface for processing incoming messages
- `IMessageProducer` - Producer interface for publishing messages
- `TransportOptions` - Base configuration options

## Usage

This package is used when implementing custom transport providers or when you need transport-agnostic code.

## Available Transports

- `Excalibur.Dispatch.Transport.RabbitMQ` - RabbitMQ transport
- `Excalibur.Dispatch.Transport.Kafka` - Apache Kafka transport
- `Excalibur.Dispatch.Transport.AzureServiceBus` - Azure Service Bus transport
- `Excalibur.Dispatch.Transport.AwsSqs` - AWS SQS transport
- `Excalibur.Dispatch.Transport.GooglePubSub` - Google Cloud Pub/Sub transport

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
