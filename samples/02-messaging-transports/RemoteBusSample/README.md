# RemoteBusSample

This sample shows how to configure Dispatch with a RabbitMQ remote bus and the Excalibur inbox and outbox processors.

## Running the sample

1. Start RabbitMQ using Docker:

```bash
docker run -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

2. In another terminal run the application:

```bash
cd samples/RemoteBusSample
 dotnet run
```

A `PingEvent` will be published through RabbitMQ via the outbox processor. If a consumer is subscribed to the queue it will receive the event.
