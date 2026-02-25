# CrossLangSample

This sample publishes a `PingEvent` using Dispatch and consumes it from Python via RabbitMQ.

## Running the sample

1. Start RabbitMQ using Docker:

```bash
docker run -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

2. Set the environment variables used by both applications:

```bash
export RabbitMq__ConnectionString=amqp://guest:guest@localhost:5672/
export RabbitMq__Exchange=dispatch
export RabbitMq__RoutingKey=dispatch.sample
```

3. In one terminal run the producer:

```bash
cd samples/CrossLangSample
 dotnet run
```

4. In another terminal install the Python dependency and start the consumer:

```bash
pip install pika
python python_consumer.py
```

The Python console will display the event message published from the .NET application, demonstrating cross-language interoperability.

## Decoding a MessagePack payload

To decode the sample MessagePack payload using Node.js:

```bash
cd samples/CrossLangSample
npm install lz4 @msgpack/msgpack
node messagepack_consumer.js
```

The script prints the deserialized `OrderCreated` object.

## Consuming MessagePack in Python

Install the additional Python dependencies and run the dedicated consumer:

```bash
pip install pika lz4 msgpack
python python_messagepack_consumer.py
```

The script decompresses the LZ4 payload and uses `msgpack` to deserialize the event, proving the schema works across runtimes.

