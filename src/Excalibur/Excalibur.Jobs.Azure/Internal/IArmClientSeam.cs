// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.ResourceManager;
using Azure.ResourceManager.Logic;

namespace Excalibur.Jobs.Azure.Internal;

/// <summary>
/// Narrow internal seam over <see cref="ArmClient"/> used by
/// <see cref="AzureLogicAppsJobProvider"/>. Exposes only the use-case
/// operations needed by the job provider so tests can substitute at this
/// boundary without faking the concrete SDK client type.
/// </summary>
/// <remarks>
/// <para>
/// The seam exposes flat use-case operations rather than mirroring the SDK's
/// client topology, and that is what makes it substitutable. The resource
/// hierarchy the SDK requires — subscription, then resource group, then the
/// workflow collection — is walked entirely behind this interface, because
/// the collection is reached through a static extension method over a
/// concrete, non-virtual SDK type: any seam that returned a handle partway
/// down that walk would hand the caller a type no test double can stand in
/// for, and the provider would once again be reachable only against live
/// Azure.
/// </para>
/// <para>
/// Only data-shaped SDK types cross the seam
/// (<see cref="LogicWorkflowData"/>) — they carry the workflow definition and
/// are constructible and inspectable by a test double.
/// </para>
/// </remarks>
internal interface IArmClientSeam
{
	/// <summary>
	/// Creates the named workflow in the given resource group, replacing it if it already exists.
	/// </summary>
	/// <param name="resourceGroupName"> The resource group that owns the workflow. </param>
	/// <param name="workflowName"> The name of the workflow. </param>
	/// <param name="workflow"> The workflow definition to apply. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> A task that completes when the workflow has been created or updated. </returns>
	Task CreateOrUpdateWorkflowAsync(
		string resourceGroupName,
		string workflowName,
		LogicWorkflowData workflow,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the named workflow from the given resource group.
	/// </summary>
	/// <param name="resourceGroupName"> The resource group that owns the workflow. </param>
	/// <param name="workflowName"> The name of the workflow. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>
	/// <see langword="true"/> when a workflow was found and deleted; <see langword="false"/> when no
	/// workflow of that name existed. Deleting an absent workflow is not an error — the caller wanted it
	/// gone and it is gone — so the distinction is reported rather than thrown.
	/// </returns>
	Task<bool> DeleteWorkflowAsync(string resourceGroupName, string workflowName, CancellationToken cancellationToken);
}
