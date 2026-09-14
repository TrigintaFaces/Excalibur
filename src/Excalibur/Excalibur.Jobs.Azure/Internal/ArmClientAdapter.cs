// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Logic;

namespace Excalibur.Jobs.Azure.Internal;

/// <summary>
/// Default <see cref="IArmClientSeam"/> implementation that forwards to a
/// real <see cref="ArmClient"/>. This adapter is the only place in the
/// jobs path that touches the live Azure Resource Manager SDK client type
/// — tests substitute at the seam, never at the SDK type directly.
/// </summary>
internal sealed class ArmClientAdapter : IArmClientSeam
{
	private readonly ArmClient _inner;

	/// <summary>
	/// Initializes a new instance of the <see cref="ArmClientAdapter"/> class.
	/// </summary>
	/// <param name="inner">The underlying Azure Resource Manager client.</param>
	public ArmClientAdapter(ArmClient inner)
	{
		ArgumentNullException.ThrowIfNull(inner);
		_inner = inner;
	}

	/// <inheritdoc/>
	public async Task CreateOrUpdateWorkflowAsync(
		string resourceGroupName,
		string workflowName,
		LogicWorkflowData workflow,
		CancellationToken cancellationToken)
	{
		var subscription = await _inner.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
		var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken)
			.ConfigureAwait(false);

		_ = await resourceGroup.Value.GetLogicWorkflows().CreateOrUpdateAsync(
			WaitUntil.Completed,
			workflowName,
			workflow,
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<bool> DeleteWorkflowAsync(
		string resourceGroupName,
		string workflowName,
		CancellationToken cancellationToken)
	{
		var subscription = await _inner.GetDefaultSubscriptionAsync(cancellationToken).ConfigureAwait(false);
		var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken)
			.ConfigureAwait(false);

		// GetIfExistsAsync, not GetLogicWorkflowAsync: the latter returns Response<T>, whose HasValue is
		// always true, so the not-found result was unreachable and a missing workflow surfaced as a thrown
		// 404 instead. Nor is a status-code catch the fix here -- this call walks subscription, then
		// resource group, then workflow, and ANY of the three can 404, so catching it as "workflow not
		// found" would report a misconfigured resource group as a successful no-op delete. The resource
		// group lookup above therefore keeps throwing: a workflow that is already gone is a normal
		// outcome, a resource group that does not exist is a configuration error, and only the
		// non-throwing variant tells the two apart.
		var workflow = await resourceGroup.Value.GetLogicWorkflows()
			.GetIfExistsAsync(workflowName, cancellationToken)
			.ConfigureAwait(false);

		if (!workflow.HasValue)
		{
			return false;
		}

		_ = await workflow.Value.DeleteAsync(WaitUntil.Completed, cancellationToken).ConfigureAwait(false);
		return true;
	}
}
