# WebWorkerSample

This sample demonstrates a minimal web API and background worker communicating through RabbitMQ.
Both hosts use Excalibur.Hosting to register the Dispatch pipeline along with the inbox and outbox processors.

## Running the sample

1. Start RabbitMQ using Docker:

```bash
docker run -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

2. In one terminal run the worker host:

```bash
cd samples/WebWorkerSample/WorkerHost
dotnet run
```

3. In another terminal start the web host:

```bash
cd samples/WebWorkerSample/WebHost
dotnet run
```

4. Send a command to the API:

```bash
curl -X POST http://localhost:5000/ping -H "Content-Type: application/json" -d '{"text":"hello"}'
```

The request is written to the outbox, forwarded to RabbitMQ and picked up by the worker host.
The handler prints the message text and returns `Pong hello` which is sent back in the HTTP response.