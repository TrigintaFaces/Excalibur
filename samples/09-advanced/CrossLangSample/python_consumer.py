import os
import pika

connection_string = os.getenv("RabbitMq__ConnectionString", "amqp://guest:guest@localhost:5672/")
exchange = os.getenv("RabbitMq__Exchange", "dispatch")
routing_key = os.getenv("RabbitMq__RoutingKey", "dispatch.sample")

params = pika.URLParameters(connection_string)
connection = pika.BlockingConnection(params)
channel = connection.channel()

channel.exchange_declare(exchange=exchange, exchange_type="direct", durable=False)
channel.queue_declare(queue=routing_key, durable=False)
channel.queue_bind(exchange=exchange, queue=routing_key, routing_key=routing_key)

print("Waiting for events...", flush=True)
for method, properties, body in channel.consume(queue=routing_key, auto_ack=True):
    print(body.decode(), flush=True)
