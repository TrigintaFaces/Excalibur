// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.ObjectModel;

using Amazon.CloudWatch.Model;

namespace Excalibur.Dispatch.Transport.Aws;

public sealed class PutMetricDataRequest
{
	public string Namespace { get; set; } = string.Empty;

	public Collection<MetricDatum> MetricData { get; } = [];
}
