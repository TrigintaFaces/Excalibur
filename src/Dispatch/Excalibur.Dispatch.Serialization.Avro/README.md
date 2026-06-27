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
**not** currently supported.

**Skew is detected and fails closed — never silently mis-decoded.** Every payload is framed with the
[Avro single-object-encoding](https://avro.apache.org/docs/current/specification/#single-object-encoding)
header: the marker `0xC3 0x01` followed by the 8-byte little-endian CRC-64-AVRO (Rabin) fingerprint of the
writer schema. On read, the serializer compares the payload's writer-schema fingerprint against the reader
schema's fingerprint; if they differ (a writer/reader schema skew) and no compatible writer schema can be
resolved, it throws `SchemaMismatchException` instead of positionally decoding against the wrong schema
(which could silently corrupt field values). A payload missing the header also fails closed.

To evolve message contracts over time, version your types explicitly (for example through the
serialization registry) so every payload is read with the schema it was written with.
