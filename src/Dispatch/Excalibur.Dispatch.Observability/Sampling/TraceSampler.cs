// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Sampling;

/// <summary>
/// Default implementation of <see cref="ITraceSampler"/> that uses the configured
/// <see cref="TraceSamplerOptions"/> to make sampling decisions.
/// </summary>
/// <param name="options">The trace sampler options.</param>
public sealed class TraceSampler(IOptions<TraceSamplerOptions> options) : ITraceSampler
{
	private readonly TraceSamplerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

	/// <inheritdoc />
	public bool ShouldSample(ActivityContext context, string name)
	{
		return _options.Strategy switch
		{
			SamplingStrategy.AlwaysOn => true,
			SamplingStrategy.AlwaysOff => false,
			SamplingStrategy.RatioBased => IsWithinRatio(),
			SamplingStrategy.ParentBased => EvaluateParentBased(context),
			_ => true,
		};
	}

	private bool EvaluateParentBased(ActivityContext context)
	{
		// If there is a parent context with a valid trace ID, inherit the parent's decision
		if (context != default && context.TraceId != default)
		{
			return context.TraceFlags.HasFlag(ActivityTraceFlags.Recorded);
		}

		// Root span: fall back to ratio-based sampling
		return IsWithinRatio();
	}

	private bool IsWithinRatio()
	{
		if (_options.SamplingRatio >= 1.0)
		{
			return true;
		}

		if (_options.SamplingRatio <= 0.0)
		{
			return false;
		}

		Span<byte> bytes = stackalloc byte[8];
		RandomNumberGenerator.Fill(bytes);
		var value = (double)BitConverter.ToUInt64(bytes) / ulong.MaxValue;
		return value < _options.SamplingRatio;
	}
}
