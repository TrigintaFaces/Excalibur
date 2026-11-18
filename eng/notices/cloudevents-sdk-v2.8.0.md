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
