# Excalibur.Dispatch.Serialization.Avro

Apache Avro serialization plugin for the Excalibur framework.

## Features

- Schema-based binary serialization via Apache Avro
- Optimized for streaming and Kafka scenarios
- Excellent schema evolution support
- Cross-language compatibility (Java, Python, Go, etc.)
- Pluggable serialization registry integration

## Usage

```csharp
// Register as a pluggable serializer (alongside default MemoryPack)
services.AddAvroPluggableSerialization();

// Or set as the current serializer for new payloads
services.AddAvroPluggableSerialization(setAsCurrent: true);

// Via the builder pattern
services.AddDispatch()
    .ConfigureSerialization(config =>
    {
        config.Register(
            AvroSerializationExtensions.GetPluggableSerializer(),
            SerializerIds.Avro);
        config.UseAvro();
    });
```

## Requirements

Types must implement `ISpecificRecord` from the Apache.Avro package.
