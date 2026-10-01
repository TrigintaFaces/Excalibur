// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Excalibur.Data.DynamoDb.Projections;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.LocalStack;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// A LocalStack DynamoDB container for the positioned-projection conformance kit.
/// </summary>
#pragma warning disable CA1812 // Instantiated by the xUnit test runner via IClassFixture<T>.
public sealed class DynamoDbPositionedProjectionContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>Gets a DynamoDB client pointing at the container.</summary>
	/// <value>The client; the store provisions its own table.</value>
	public IAmazonDynamoDB Client => _client
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-positioned-projection-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var credentials = new BasicAWSCredentials("test", "test");
		var config = new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() };
		_client = new AmazonDynamoDBClient(credentials, config);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			_client?.Dispose();

			if (_container is not null)
			{
				await _container.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Best effort - allow the test host to exit cleanly.
		}
	}
}
#pragma warning restore CA1812

/// <summary>
/// Runs the positioned-projection conformance kit against a REAL DynamoDB service.
/// </summary>
/// <remarks>
/// <para>
/// <b>This provider expresses its whole contract as a condition expression, which is exactly the kind of
/// thing that cannot be tested without the service.</b> A condition expression is validated server-side
/// before it is evaluated, and the failure mode is not a wrong answer — it is a request the service
/// refuses to consider at all. That refusal arrives as <c>ValidationException</c>, which is not the
/// <c>ConditionalCheckFailedException</c> the store catches, so it escapes the apply delegate and halts
/// the subscription rather than being reported as an outcome.
/// </para>
/// <para>
/// Specifically: DynamoDB rejects a request that carries an expression attribute value no expression
/// references, and equally one carrying an unreferenced NAME. The create-if-absent branch's condition
/// references only the partition key, so building either map up front made every first write fail.
/// Nothing short of the real service sees that —
/// the C# is correct, the expression is correct, and the pairing is not.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Infrastructure", TestInfrastructure.DynamoDb)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DynamoDbPositionedProjectionConformanceTests
	: PositionedProjectionStoreConformanceTestKit,
		IClassFixture<DynamoDbPositionedProjectionContainerFixture>
{
	private readonly DynamoDbPositionedProjectionContainerFixture _fixture;
	private readonly string _tableName = $"positioned_projection_{Guid.NewGuid():N}";

	public DynamoDbPositionedProjectionConformanceTests(DynamoDbPositionedProjectionContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>Wires the kit's own completeness check, which names any arm this suite forgot.</summary>
	/// <returns>A task representing the check.</returns>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public Task Create_when_nothing_is_stored_Test() => Create_when_nothing_is_stored();

	[Fact]
	public Task Refuse_a_late_starter_that_claims_absence_Test() => Refuse_a_late_starter_that_claims_absence();

	[Fact]
	public Task Refuse_a_row_that_carries_no_position_Test() => Refuse_a_row_that_carries_no_position();

	// The liveness half of the pair above, and what makes that refusal legitimate rather than a stall:
	// without it the refusal is satisfied by a store that refuses everything.
	[Fact]
	public Task Rebuild_repairs_a_row_that_carries_no_position_Test() =>
		Rebuild_repairs_a_row_that_carries_no_position();

	// The READ side of the two no-number states. Both writes are refused; what differs is which state
	// the row reads back as -- UNPLACEABLE here, UNNUMBERED above -- and a store that dropped the
	// position instead of recording the sentinel passes the refusal and fails this.
	[Fact]
	public Task Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable_Test() =>
		Refuse_to_adopt_a_row_an_unconditional_write_left_unplaceable();

	[Fact]
	public Task Advance_from_the_position_it_read_Test() => Advance_from_the_position_it_read();

	[Fact]
	public Task Refuse_a_stale_expected_position_Test() => Refuse_a_stale_expected_position();

	[Fact]
	public Task Refuse_a_position_that_does_not_advance_Test() => Refuse_a_position_that_does_not_advance();

	[Fact]
	public Task Report_a_deleted_projection_as_vanished_without_recreating_it_Test() =>
		Report_a_deleted_projection_as_vanished_without_recreating_it();

	[Fact]
	public Task Admit_exactly_one_of_two_writers_racing_from_one_read_Test() =>
		Admit_exactly_one_of_two_writers_racing_from_one_read();

	[Fact]
	public Task Invalidate_the_position_when_an_unconditional_write_replaces_the_state_Test() =>
		Invalidate_the_position_when_an_unconditional_write_replaces_the_state();

	[Fact]
	public Task Read_the_state_and_its_position_as_one_observation_Test() =>
		Read_the_state_and_its_position_as_one_observation();

	/// <summary>
	/// The RE-FOLD arms. Erasure mutates events in place, so the fold beneath a position changes while
	/// the position stays fixed — a state transition the advancing write cannot express.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refold_the_state_at_the_position_the_row_already_holds_Test() =>
		Refold_the_state_at_the_position_the_row_already_holds();

	/// <summary>Refuses a re-fold the row moved out from under.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_refold_when_the_row_advanced_after_the_read_Test() =>
		Refuse_a_refold_when_the_row_advanced_after_the_read();

	/// <summary>Refuses a re-fold naming a position the row does not hold.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_refold_at_a_position_the_row_does_not_hold_Test() =>
		Refuse_a_refold_at_a_position_the_row_does_not_hold();

	/// <summary>An unpositioned row is terminal, not retryable.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_requires_rebuild_for_a_row_with_no_established_position_Test() =>
		Report_requires_rebuild_for_a_row_with_no_established_position();

	// The UNNUMBERED half of the pair above: that arm drives the row through the blind surface, this one
	// writes a complete fold with no number. A store discriminating on == Unplaceable rather than
	// != Positioned passes one and fails the other.
	[Fact]
	public Task Report_requires_rebuild_for_a_refold_against_an_unnumbered_row_Test() =>
		Report_requires_rebuild_for_a_refold_against_an_unnumbered_row();

	/// <summary>A repeated re-fold leaves the row identical.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Apply_the_same_refold_twice_without_changing_the_result_Test() =>
		Apply_the_same_refold_twice_without_changing_the_result();

	/// <summary>A re-fold must never resurrect a row erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Report_a_refold_against_an_absent_row_as_vanished_without_creating_it_Test() =>
		Report_a_refold_against_an_absent_row_as_vanished_without_creating_it();

	/// <summary>A negative position is refused at the entry point, not stored.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Refuse_a_negative_position_argument_Test() => Refuse_a_negative_position_argument();

	/// <summary>A rebuild must never resurrect a row an erasure deleted.</summary>
	/// <returns>A task representing the arm.</returns>
	[Fact]
	public Task Rebuild_must_not_resurrect_a_deleted_row_Test() => Rebuild_must_not_resurrect_a_deleted_row();

	/// <inheritdoc />
	protected override Task<IProjectionStore<ConformanceProjection>> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a DynamoDB container must be available -- positioned-projection conformance is never "
			+ "skipped, because an arm that passes by being skipped is indistinguishable from one that "
			+ "passed by working, and this provider's contract is enforced entirely by the service.");

		var options = Microsoft.Extensions.Options.Options.Create(new DynamoDbProjectionStoreOptions
		{
			TableName = _tableName,
			AutoCreateTable = true,
		});

		return Task.FromResult<IProjectionStore<ConformanceProjection>>(
			new DynamoDbProjectionStore<ConformanceProjection>(
				_fixture.Client,
				options,
				NullLogger<DynamoDbProjectionStore<ConformanceProjection>>.Instance));
	}
}
