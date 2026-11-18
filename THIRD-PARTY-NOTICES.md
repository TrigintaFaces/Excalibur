# THIRD-PARTY NOTICES

This file lists every third-party package that the shipping projects in this repository
declare: the packages their project files reference, plus the transitive dependencies that
central package pinning promotes into each published package's own dependency list. It is
generated from those project files and their committed lock files, so it matches what a
consumer's restore resolves. Licenses remain with their respective owners.

Licenses are recorded per package id in `eng/ci/package-licenses.json`. Most are SPDX
expressions taken from the package's own metadata. Where a package ships its terms as a
license file or a URL instead of an SPDX expression, the entry states those terms in words,
and is marked PROPRIETARY when they are not an OSI-approved open-source license.

| Package | Version | License |
|---------|---------|---------|
| AWSSDK.CloudWatch | 4.0.9.4 | Apache-2.0 |
| AWSSDK.Core | 4.0.3.30 | Apache-2.0 |
| AWSSDK.DynamoDBStreams | 4.0.4.25 | Apache-2.0 |
| AWSSDK.DynamoDBv2 | 4.0.17.9 | Apache-2.0 |
| AWSSDK.EventBridge | 4.0.5.26 | Apache-2.0 |
| AWSSDK.Extensions.NETCore.Setup | 4.0.3.33 | Apache-2.0 |
| AWSSDK.KeyManagementService | 4.0.9.11 | Apache-2.0 |
| AWSSDK.S3 | 4.0.21.2 | Apache-2.0 |
| AWSSDK.SQS | 4.0.2.25 | Apache-2.0 |
| AWSSDK.Scheduler | 4.0.2.24 | Apache-2.0 |
| AWSSDK.SecretsManager | 4.0.4.17 | Apache-2.0 |
| AWSSDK.SimpleNotificationService | 4.0.2.27 | Apache-2.0 |
| Amazon.Extensions.Configuration.SystemsManager | 7.1.0 | Apache-2.0 |
| Amazon.Lambda.APIGatewayEvents | 2.7.3 | Apache-2.0 |
| Amazon.Lambda.Core | 2.8.1 | Apache-2.0 |
| Amazon.Lambda.DynamoDBEvents | 3.1.2 | Apache-2.0 |
| Amazon.Lambda.KinesisEvents | 3.0.0 | Apache-2.0 |
| Amazon.Lambda.RuntimeSupport | 1.14.3 | Apache-2.0 |
| Amazon.Lambda.SNSEvents | 2.1.1 | Apache-2.0 |
| Amazon.Lambda.SQSEvents | 2.2.1 | Apache-2.0 |
| Amazon.Lambda.Serialization.SystemTextJson | 2.4.5 | Apache-2.0 |
| Apache.Avro | 1.12.1 | Apache-2.0 |
| Asp.Versioning.Mvc | 8.1.1 | MIT |
| Asp.Versioning.Mvc.ApiExplorer | 8.1.1 | MIT |
| AspNetCore.HealthChecks.MongoDb | 8.0.0 | Apache-2.0 |
| AspNetCore.HealthChecks.Npgsql | 9.0.0 | Apache-2.0 |
| AspNetCore.HealthChecks.Redis | 9.0.0 | Apache-2.0 |
| AspNetCore.HealthChecks.SqlServer | 9.0.0 | Apache-2.0 |
| AspNetCore.HealthChecks.System | 9.0.0 | Apache-2.0 |
| Azure.Identity | 1.21.0 | MIT |
| Azure.Messaging.EventGrid | 5.0.0 | MIT |
| Azure.Messaging.EventHubs | 5.12.2 | MIT |
| Azure.Messaging.EventHubs.Processor | 5.12.2 | MIT |
| Azure.Messaging.ServiceBus | 7.20.1 | MIT |
| Azure.ResourceManager | 1.14.0 | MIT |
| Azure.ResourceManager.Logic | 1.2.0-beta.1 | MIT |
| Azure.Security.KeyVault.Keys | 4.9.0 | MIT |
| Azure.Security.KeyVault.Secrets | 4.10.0 | MIT |
| Azure.Storage.Blobs | 12.27.0 | MIT |
| Azure.Storage.Queues | 12.25.0 | MIT |
| Ben.Demystifier | 0.4.1 | Apache-2.0 |
| CloudNative.CloudEvents | 2.8.0 | Apache-2.0 |
| CloudNative.CloudEvents.SystemTextJson | 2.8.0 | Apache-2.0 |
| Confluent.Kafka | 2.14.0 | Apache-2.0 |
| Confluent.SchemaRegistry | 2.14.0 | Apache-2.0 |
| Consul | 1.8.0 | Apache-2.0 |
| Cronos | 0.12.0 | MIT |
| Dapper | 2.1.89 | Apache-2.0 |
| DotPulsar | 5.3.0 | Apache-2.0 |
| Elastic.Clients.Elasticsearch | 9.5.3 | Apache-2.0 |
| Elastic.Transport | 1.1.0 | Apache-2.0 |
| FluentValidation | 12.1.1 | Apache-2.0 |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Apache-2.0 |
| Google.Api.Gax.Grpc | 4.13.1 | BSD-3-Clause |
| Google.Apis.Auth | 1.73.0 | Apache-2.0 |
| Google.Cloud.Firestore | 4.2.0 | Apache-2.0 |
| Google.Cloud.Functions.Framework | 3.0.1 | Apache-2.0 |
| Google.Cloud.Functions.Hosting | 3.0.1 | Apache-2.0 |
| Google.Cloud.Monitoring.V3 | 3.16.0 | Apache-2.0 |
| Google.Cloud.PubSub.V1 | 3.33.0 | Apache-2.0 |
| Google.Cloud.Scheduler.V1 | 3.6.0 | Apache-2.0 |
| Google.Cloud.Spanner.Data | 4.6.0 | Apache-2.0 |
| Google.Cloud.Storage.V1 | 4.14.0 | Apache-2.0 |
| Google.Events.Protobuf | 1.8.0 | Apache-2.0 |
| Google.Protobuf | 3.34.1 | BSD-3-Clause |
| Grpc.Net.Client | 2.76.0 | Apache-2.0 |
| IBMMQDotnetClient | 10.0.0 | PROPRIETARY - IBM International License Agreement (https://www.ibm.com/support/customer/csol/terms/?id=L-MKDD-7KHY2Q) |
| IdentityModel | 7.0.0 | Apache-2.0 |
| JsonNet.ContractResolvers | 2.0.0 | MIT |
| Konscious.Security.Cryptography.Argon2 | 1.3.1 | MIT |
| KubernetesClient | 19.0.2 | Apache-2.0 |
| MQTTnet | 5.2.0.1603 | MIT |
| Marten | 9.13.0 | MIT |
| Medo.Uuid7 | 3.2.0 | MIT |
| MemoryPack | 1.21.4 | MIT |
| MessagePack | 3.1.9 | MIT |
| Microsoft.ApplicationInsights | 3.1.0 | MIT |
| Microsoft.ApplicationInsights.AspNetCore | 3.1.0 | MIT |
| Microsoft.AspNetCore.Authorization | 10.0.13 | MIT |
| Microsoft.AspNetCore.DataProtection | 10.0.13 | MIT |
| Microsoft.Azure.Cosmos | 3.58.0 | MIT |
| Microsoft.Azure.Functions.Worker | 2.51.0 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.CosmosDB | 4.14.0 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.EventGrid | 3.6.0 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.EventHubs | 6.5.0 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.Http | 3.3.0 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.ServiceBus | 5.24.0 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs | 6.8.1 | MIT |
| Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues | 5.5.4 | MIT |
| Microsoft.CodeAnalysis.Analyzers | 5.3.0 | MIT |
| Microsoft.CodeAnalysis.Common | 5.3.0 | MIT |
| Microsoft.Data.SqlClient | 7.1.1 | MIT |
| Microsoft.Data.Sqlite | 10.0.13 | MIT |
| Microsoft.Extensions.Caching.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.Caching.Hybrid | 10.5.0 | MIT |
| Microsoft.Extensions.Caching.Memory | 10.0.13 | MIT |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.13 | MIT |
| Microsoft.Extensions.Configuration | 10.0.13 | MIT |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.Configuration.Binder | 10.0.13 | MIT |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.13 | MIT |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.13 | MIT |
| Microsoft.Extensions.Configuration.Json | 10.0.13 | MIT |
| Microsoft.Extensions.DependencyInjection | 10.0.13 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.Diagnostics.HealthChecks | 10.0.13 | MIT |
| Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.FileProviders.Embedded | 10.0.13 | MIT |
| Microsoft.Extensions.Hosting | 10.0.13 | MIT |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.Http | 10.0.13 | MIT |
| Microsoft.Extensions.Http.Resilience | 10.8.0 | MIT |
| Microsoft.Extensions.Logging | 10.0.13 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.13 | MIT |
| Microsoft.Extensions.Logging.Console | 10.0.13 | MIT |
| Microsoft.Extensions.ObjectPool | 10.0.13 | MIT |
| Microsoft.Extensions.Options | 10.0.13 | MIT |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.13 | MIT |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.13 | MIT |
| Microsoft.IdentityModel.Tokens | 8.17.0 | MIT |
| Microsoft.OpenApi | 2.7.5 | MIT |
| MongoDB.Driver | 3.11.1 | Apache-2.0 |
| MySqlConnector | 2.6.1 | MIT |
| NCrontab | 3.4.0 | Apache-2.0 |
| Npgsql | 10.0.2 | PostgreSQL |
| OpenSearch.Client | 1.8.0 | Apache-2.0 |
| OpenTelemetry | 1.15.3 | Apache-2.0 |
| OpenTelemetry.Api | 1.15.3 | Apache-2.0 |
| OpenTelemetry.Exporter.Console | 1.15.3 | Apache-2.0 |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.15.3 | Apache-2.0 |
| OpenTelemetry.Exporter.Prometheus.AspNetCore | 1.13.0-beta.1 | Apache-2.0 |
| OpenTelemetry.Extensions.Hosting | 1.15.3 | Apache-2.0 |
| OpenTelemetry.Extensions.Propagators | 1.15.3 | Apache-2.0 |
| OpenTelemetry.Instrumentation.AspNetCore | 1.15.1 | Apache-2.0 |
| OpenTelemetry.Instrumentation.Http | 1.15.0 | Apache-2.0 |
| OpenTelemetry.Instrumentation.Process | 1.12.0-beta.1 | Apache-2.0 |
| OpenTelemetry.Instrumentation.Runtime | 1.15.0 | Apache-2.0 |
| Oracle.ManagedDataAccess.Core | 23.8.0 | PROPRIETARY - Oracle Free Distribution, Hosting, and Use Terms and Conditions (not an OSI-approved licence) |
| Polly | 8.6.6 | BSD-3-Clause |
| Polly.RateLimiting | 8.6.6 | BSD-3-Clause |
| Quartz | 4.3.0 | Apache-2.0 |
| Quartz.Plugins | 4.3.0 | Apache-2.0 |
| QuestPDF | 2026.7.3 | Source-available dual licence (QuestPDF Community, or paid Professional/Enterprise) — NOT MIT and not OSI-approved: the shipped licence states that the MIT License does not govern use under the Community License. Community eligibility is assessed per organisation across six categories, not revenue alone — public-sector entities, government agencies and publicly traded companies are ineligible regardless of revenue, and the small-business category requires annual gross revenue under USD 1,000,000 measured on a consolidated basis across entities under common control (https://www.questpdf.com/license/). Reached only through the opt-in `Excalibur.Compliance.Pdf` package. Eligibility attaches to the organisation using the software, so each consumer must qualify on its own account; this project cannot and does not qualify on your behalf. |
| RabbitMQ.Client | 7.2.1 | Apache-2.0 OR MPL-2.0 |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.3 | Apache-2.0 |
| SQLitePCLRaw.core | 3.0.3 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 3.0.3 | Apache-2.0 |
| SSH.NET | 2026.0.0 | MIT |
| Serilog | 4.3.1 | Apache-2.0 |
| Serilog.AspNetCore | 10.0.0 | Apache-2.0 |
| Serilog.Extensions.Hosting | 10.0.0 | Apache-2.0 |
| Serilog.Extensions.Logging | 10.0.0 | Apache-2.0 |
| Serilog.Settings.Configuration | 10.0.0 | Apache-2.0 |
| Serilog.Sinks.Console | 6.1.1 | Apache-2.0 |
| Serilog.Sinks.Debug | 3.0.0 | Apache-2.0 |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 |
| SharpCompress | 0.48.1 | MIT |
| Shouldly | 4.3.0 | BSD-3-Clause |
| Snappier | 1.3.1 | BSD-3-Clause |
| StackExchange.Redis | 2.12.14 | MIT |
| Swashbuckle.AspNetCore.SwaggerGen | 10.2.3 | MIT |
| System.Collections.Immutable | 10.0.13 | MIT |
| System.CommandLine | 2.0.0-beta4.22272.1 | MIT |
| System.Diagnostics.DiagnosticSource | 10.0.13 | MIT |
| System.Diagnostics.PerformanceCounter | 10.0.13 | MIT |
| System.Formats.Cbor | 10.0.15 | MIT |
| System.IO.Hashing | 10.0.13 | MIT |
| System.IdentityModel.Tokens.Jwt | 8.17.0 | MIT |
| System.Security.Cryptography.Pkcs | 10.0.13 | MIT |
| System.Security.Cryptography.Xml | 10.0.13 | MIT |
| System.Text.Json | 10.0.13 | MIT |
| System.Threading.Channels | 10.0.13 | MIT |
| System.Threading.RateLimiting | 10.0.13 | MIT |
| Testcontainers | 4.11.0 | MIT |
| Testcontainers.CosmosDb | 4.11.0 | MIT |
| Testcontainers.MsSql | 4.11.0 | MIT |
| Testcontainers.PostgreSql | 4.11.0 | MIT |
| Testcontainers.RabbitMq | 4.11.0 | MIT |
| Testcontainers.Redis | 4.11.0 | MIT |
| VaultSharp | 1.17.5.1 | Apache-2.0 |
| YamlDotNet | 17.0.1 | MIT |
| xunit.v3.extensibility.core | 4.0.1 | Apache-2.0 |

## Native components reached through Confluent.Kafka

The `Confluent.Kafka` package contains managed assemblies only. The native Kafka client it
calls arrives as a separate NuGet package, `librdkafka.redist`, which your restore resolves
transitively -- no package in this repository declares it, which is why it has no row above.

`librdkafka.redist` ships prebuilt native binaries and states no SPDX license expression.
Its terms are published as a single `LICENSES.txt` covering librdkafka itself, which is
BSD-2-Clause, together with the third-party components built into those binaries. At the
version this repository resolves, that file names fourteen: cjson, crc32c, fnv1a,
hdrhistogram, lz4, murmur2, nanopb, opentelemetry, pycrc, queue, regexp, snappy, tinycthread
and wingetopt. Read it at the version your build resolves, not at the package's own license
link, which points at a moving branch:

    https://github.com/confluentinc/librdkafka/blob/v2.14.0/LICENSES.txt

## License notice for CloudEvents C# SDK v2.8.0

Upstream: CloudEvents C# SDK v2.8.0
Commit: 54cbd3ca8576ba92fe441eb86e0c85f0e67d1eb7
Copyright (c) Cloud Native Foundation.
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

The complete unchanged license text also ships as licenses/LICENSE-APACHE-2.0.txt in every
package, because section 4(a) asks that a copy of the License accompany a derivative work and a
link is not a copy. Apache-2.0 is NOT one of the licenses this software is offered under; it
cannot be elected. See LICENSE.

Adapted repository source:
src/Dispatch/Excalibur.Dispatch/CloudEvents/MetadataCloudEventFormatter.cs

Pinned upstream source:
https://github.com/cloudevents/sdk-csharp/blob/54cbd3ca8576ba92fe441eb86e0c85f0e67d1eb7/src/CloudNative.CloudEvents.SystemTextJson/JsonEventFormatter.cs
SHA256 7B9EC4386A6C915DAD8EBDCB54E027B652E6F6C358238FFDE05AF6C13457BC1D

Adapted portions and upstream source line ranges (at that exact commit):
- public override CloudEvent DecodeStructuredModeMessage(ReadOnlyMemory<byte> <- SDK54cbd JsonEventFormatter.cs:135-136
- public override CloudEvent DecodeStructuredModeMessage(Stream <- SDK54cbd JsonEventFormatter.cs:131-132
- public override async Task<CloudEvent> DecodeStructuredModeMessageAsync <- SDK54cbd JsonEventFormatter.cs:127-128, SDK54cbd JsonEventFormatter.cs:138-146
- public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(ReadOnlyMemory<byte> <- SDK54cbd JsonEventFormatter.cs:157-158
- public override IReadOnlyList<CloudEvent> DecodeBatchModeMessage(Stream <- SDK54cbd JsonEventFormatter.cs:153-154
- public override async Task<IReadOnlyList<CloudEvent>> DecodeBatchModeMessageAsync <- SDK54cbd JsonEventFormatter.cs:149-150, SDK54cbd JsonEventFormatter.cs:169-188
- private static IReadOnlyList<CloudEvent> DecodeBatchDocument <- SDK54cbd JsonEventFormatter.cs:169-188
- private static JsonDocument ReadDocument( <- SDK54cbd JsonEventFormatter.cs:190-207
- private static async Task<JsonDocument> ReadDocumentAsync( <- SDK54cbd JsonEventFormatter.cs:190-207
- private static CloudEvent DecodeJsonElement( <- SDK54cbd JsonEventFormatter.cs:209-228
- private static void PopulateAttributesFromStructuredEvent( <- SDK54cbd JsonEventFormatter.cs:230-275
- private static void ValidateTokenTypeForAttribute( <- SDK54cbd JsonEventFormatter.cs:277-303
- private static void PopulateDataFromStructuredEvent( <- SDK54cbd JsonEventFormatter.cs:305-337
- private static void DecodeStructuredModeDataBase64Property( <- SDK54cbd JsonEventFormatter.cs:357-364
- private static void DecodeStructuredModeDataProperty( <- SDK54cbd JsonEventFormatter.cs:387-401
- public override ReadOnlyMemory<byte> EncodeBatchModeMessage <- SDK54cbd JsonEventFormatter.cs:404-423
- public override ReadOnlyMemory<byte> EncodeStructuredModeMessage <- SDK54cbd JsonEventFormatter.cs:426-438
- private Utf8JsonWriter CreateUtf8JsonWriter <- SDK54cbd JsonEventFormatter.cs:461-471
- private void WriteCloudEventForBatchOrStructuredMode <- SDK54cbd JsonEventFormatter.cs:473-511
- protected override string? InferDataContentType <- SDK54cbd JsonEventFormatter.cs:520-520
- public override ReadOnlyMemory<byte> EncodeBinaryModeEventData <- SDK54cbd JsonEventFormatter.cs:566-598
- private void EncodeStructuredModeData <- SDK54cbd JsonEventFormatter.cs:535-563
- public override void DecodeBinaryModeEventData <- SDK54cbd JsonEventFormatter.cs:601-632
- private static bool IsJsonMediaType <- SDK54cbd JsonEventFormatter.cs:641-641

- private static bool IsTextMediaType <- SDK54cbd JsonEventFormatter.cs:601-632 (text prefix branch; exact current-culture behavior)

Modifications: the internal formatter directly implements the abstract SDK
CloudEventFormatter, without constructing/inheriting/delegating to its concrete
JsonEventFormatter. Envelope/batch/decoder behavior is retained. JSON payload
encoding uses the existing Excalibur same-options JsonTypeInfo<object> hooks,
including converter/ancestor/Slim behavior. IO wrappers separate synchronous and
asynchronous parsing while preserving SDK2.8 stream/BOM/charset ownership and
errors; internal buffers/writers are disposed. Safe SDK Core validation, MIME,
binary-data and public attribute-ordinal APIs are reused. No SDK2.9/main behavior
or dependency version is substituted.

Upstream-derived portions retain Apache-2.0 terms and copyright. Excalibur
contributions retain the repository terms identified in the source header.
The repository header does not replace or relicense upstream code. Apache-2.0
is not among the licences this project offers; this notice records third-party
attribution for the adapted portions only, and electing it is not available.

This notice is a section of the generated root THIRD-PARTY-NOTICES.md, which
ships in every package. Its presence in a package is not a statement that the
package uses this SDK -- only Excalibur.Dispatch contains the adapted source.

The complete nontruncated upstream source tree at the pinned commit has no
NOTICE-named source file; both pinned SDK2.8 package archives contain LICENSE
and no NOTICE. This file records repository adaptation provenance; it does not
replace a nonexistent upstream NOTICE or remove any upstream terms.
