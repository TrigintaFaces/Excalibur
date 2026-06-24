# Excalibur.Dispatch.Serialization.Avro

Apache Avro serialization plugin for the Excalibur framework.

## Features

- Schema-based binary serialization via Apache Avro
- Optimized for streaming and Kafka scenarios
- Cross-language compatibility (Java, Python, Go, etc.) when reader and writer share the same schema
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

## Schema Compatibility

This serializer decodes each payload using the reader type's own schema as **both** the reader and writer
schema, so data must have been written with the **same** schema the reader expects. Avro writer-schema
resolution — schema evolution, i.e. reading data written with an older but compatible writer schema — is
**not** currently supported: a writer/reader schema skew produces a deserialization error rather than a
silent mis-decode.

To evolve message contracts over time, version your types explicitly (for example through the
serialization registry) so every payload is read with the schema it was written with.
