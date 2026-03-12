// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Jobs.Azure.Diagnostics;

/// <summary>
/// Event IDs for Azure Logic Apps Job Provider (146100-146199).
/// </summary>
internal static class AzureJobsEventId
{
	/// <summary>Azure Logic Apps workflow created successfully.</summary>
	public const int AzureLogicAppsWorkflowCreated = 146100;

	/// <summary>Azure Logic Apps workflow creation failed.</summary>
	public const int AzureLogicAppsWorkflowCreationFailed = 146101;

	/// <summary>Azure Logic Apps workflow deleted successfully.</summary>
	public const int AzureLogicAppsWorkflowDeleted = 146102;

	/// <summary>Azure Logic Apps workflow not found for deletion.</summary>
	public const int AzureLogicAppsWorkflowNotFound = 146103;

	/// <summary>Azure Logic Apps workflow deletion failed.</summary>
	public const int AzureLogicAppsWorkflowDeletionFailed = 146104;
}
