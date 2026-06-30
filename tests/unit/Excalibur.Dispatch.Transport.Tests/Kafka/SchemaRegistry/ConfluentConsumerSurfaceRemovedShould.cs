// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Structural regression lock: the dead Confluent Schema Registry consumer-side cluster is removed
/// from the Kafka transport surface, while the live producer-side capability remains intact.
/// </summary>
/// <remarks>
/// The consumer-side options/processor cluster was advertised public but never wired (no DI
/// registration, no entry point). It was removed. These reflection checks fail (RED) if the removed
/// types reappear, and the negative-control checks fail if the kept producer-side surface is dropped —
/// so this lock is non-vacuous against a no-op or an over-broad removal.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class ConfluentConsumerSurfaceRemovedShould
{
	private static readonly System.Reflection.Assembly KafkaAssembly = typeof(ConfluentWireFormat).Assembly;

	private const string Ns = "Excalibur.Dispatch.Transport.Kafka";

	[Theory]
	[InlineData("ConfluentConsumerOptions")]
	[InlineData("DeserializationErrorHandling")]
	[InlineData("ProcessingStatus")]
	public void Not_expose_the_removed_consumer_side_type(string typeName)
	{
		KafkaAssembly.GetType($"{Ns}.{typeName}", throwOnError: false)
			.ShouldBeNull($"{typeName} is dead consumer-side surface and must stay removed");
	}

	[Theory]
	[InlineData("ConfluentWireFormat")]
	[InlineData("SubjectNameStrategy")]
	[InlineData("CompatibilityMode")]
	public void Keep_the_live_producer_side_type(string typeName)
	{
		// Negative control: the producer-side capability is untouched by the consumer removal.
		KafkaAssembly.GetType($"{Ns}.{typeName}", throwOnError: false)
			.ShouldNotBeNull($"{typeName} is live producer-side surface and must remain");
	}

	[Fact]
	public void Keep_the_AddConfluentSchemaRegistry_producer_entry_point()
	{
		var extensions = KafkaAssembly.GetType(
			"Microsoft.Extensions.DependencyInjection.SchemaRegistryServiceCollectionExtensions", throwOnError: false);
		extensions.ShouldNotBeNull("SchemaRegistryServiceCollectionExtensions must remain (producer-side entry point)");
		extensions!
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.ShouldContain(m => m.Name == "AddConfluentSchemaRegistry",
				"AddConfluentSchemaRegistry producer entry point must remain");
	}
}
