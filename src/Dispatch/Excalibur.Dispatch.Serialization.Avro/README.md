# Excalibur.Dispatch.Serialization.Avro

Apache Avro serialization plugin for the Excalibur framework.

## Features

- Schema-based binary serialization via Apache Avro
- Optimized for streaming and Kafka scenarios
- Excellent schema evolution support
- Cross-language compatibility (Java, Python, Go, etc.)
- Pluggable serialization registry integration

## Usage

One call does everything -- DI registration, serializer registry entry, and setting Avro as the current serializer:

```csharp
services.AddAvroSerializer();
```

With custom options:

```csharp
services.AddAvroSerializer(opts =>
{
    opts.BufferSize = 8192; // default: 4096
});
```

## Requirements

Types must implement `ISpecificRecord` from the Apache.Avro package.
