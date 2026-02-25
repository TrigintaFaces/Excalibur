using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.EventSourcing;

/// <summary>
/// Contract tests for ISnapshotStore interface.
/// Verifies the interface defines all required methods for snapshot operations.
/// </summary>
[Trait("Category", "Unit")]
public class ISnapshotStoreContractShould
{
	[Fact]
	public void Have_GetLatestSnapshotAsync_Method()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);

		// Act
		var method = interfaceType.GetMethod(nameof(ISnapshotStore.GetLatestSnapshotAsync));

		// Assert
		_ = method.ShouldNotBeNull("ISnapshotStore must define GetLatestSnapshotAsync method");
		method.ReturnType.ShouldBe(typeof(ValueTask<ISnapshot?>), "GetLatestSnapshotAsync must return ValueTask<ISnapshot?>");

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(3, "GetLatestSnapshotAsync must have 3 parameters");
		parameters[0].ParameterType.ShouldBe(typeof(string), "First parameter must be aggregateId (string)");
		parameters[1].ParameterType.ShouldBe(typeof(string), "Second parameter must be aggregateType (string)");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken), "Third parameter must be cancellationToken");
	}

	[Fact]
	public void Have_SaveSnapshotAsync_Method()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);

		// Act
		var method = interfaceType.GetMethod(nameof(ISnapshotStore.SaveSnapshotAsync));

		// Assert
		_ = method.ShouldNotBeNull("ISnapshotStore must define SaveSnapshotAsync method");
		method.ReturnType.ShouldBe(typeof(ValueTask), "SaveSnapshotAsync must return ValueTask");

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(2, "SaveSnapshotAsync must have 2 parameters");
		parameters[0].ParameterType.ShouldBe(typeof(ISnapshot), "First parameter must be snapshot (ISnapshot)");
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken), "Second parameter must be cancellationToken");
	}

	[Fact]
	public void Have_DeleteSnapshotsAsync_Method()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);

		// Act
		var method = interfaceType.GetMethod(nameof(ISnapshotStore.DeleteSnapshotsAsync));

		// Assert
		_ = method.ShouldNotBeNull("ISnapshotStore must define DeleteSnapshotsAsync method");
		method.ReturnType.ShouldBe(typeof(ValueTask), "DeleteSnapshotsAsync must return ValueTask");

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(3, "DeleteSnapshotsAsync must have 3 parameters");
		parameters[0].ParameterType.ShouldBe(typeof(string), "First parameter must be aggregateId (string)");
		parameters[1].ParameterType.ShouldBe(typeof(string), "Second parameter must be aggregateType (string)");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken), "Third parameter must be cancellationToken");
	}

	[Fact]
	public void Have_DeleteSnapshotsOlderThanAsync_Method()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);

		// Act
		var method = interfaceType.GetMethod(nameof(ISnapshotStore.DeleteSnapshotsOlderThanAsync));

		// Assert
		_ = method.ShouldNotBeNull("ISnapshotStore must define DeleteSnapshotsOlderThanAsync method");
		method.ReturnType.ShouldBe(typeof(ValueTask), "DeleteSnapshotsOlderThanAsync must return ValueTask");

		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(4, "DeleteSnapshotsOlderThanAsync must have 4 parameters");
		parameters[0].ParameterType.ShouldBe(typeof(string), "First parameter must be aggregateId (string)");
		parameters[1].ParameterType.ShouldBe(typeof(string), "Second parameter must be aggregateType (string)");
		parameters[2].ParameterType.ShouldBe(typeof(long), "Third parameter must be olderThanVersion (long)");
		parameters[3].ParameterType.ShouldBe(typeof(CancellationToken), "Fourth parameter must be cancellationToken");
	}

	[Fact]
	public void Support_Snapshot_Cleanup_Via_Delete_Methods()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);

		// Act
		var deleteAllMethod = interfaceType.GetMethod(nameof(ISnapshotStore.DeleteSnapshotsAsync));
		var deleteOlderMethod = interfaceType.GetMethod(nameof(ISnapshotStore.DeleteSnapshotsOlderThanAsync));

		// Assert
		_ = deleteAllMethod.ShouldNotBeNull("ISnapshotStore must define DeleteSnapshotsAsync for cleanup");
		_ = deleteOlderMethod.ShouldNotBeNull("ISnapshotStore must define DeleteSnapshotsOlderThanAsync for version-based cleanup");
	}

	[Fact]
	public void Support_Nullable_Snapshot_For_Missing_Snapshots()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);
		var getLatestMethod = interfaceType.GetMethod(nameof(ISnapshotStore.GetLatestSnapshotAsync));

		// Act
		var returnType = getLatestMethod.ReturnType;

		// Assert
		returnType.ShouldBe(typeof(ValueTask<ISnapshot?>), "GetLatestSnapshotAsync must return nullable ISnapshot to handle missing snapshots");
	}

	[Fact]
	public void All_Methods_Support_Cancellation()
	{
		// Arrange
		var interfaceType = typeof(ISnapshotStore);
		var methods = interfaceType.GetMethods();

		// Act & Assert
		foreach (var method in methods)
		{
			var parameters = method.GetParameters();
			var hasCancellationToken = parameters.Any(p => p.ParameterType == typeof(CancellationToken));

			hasCancellationToken.ShouldBeTrue($"Method {method.Name} must support cancellation via CancellationToken parameter");
		}
	}
}
