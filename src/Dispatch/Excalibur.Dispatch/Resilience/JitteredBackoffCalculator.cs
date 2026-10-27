// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Randomises another calculator's delay, sampling uniformly from zero up to that calculator's nominal
/// value.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only jitter implementation in the package, and it used to be one of four.</b> Jitter was
/// previously a boolean flag with a factor inside the exponential calculator, a decorator here, and the
/// defining behaviour of two further strategy types -- four mechanisms for one concept, with four
/// slightly different distributions and nothing stating which a consumer would get. Now a
/// <see cref="BackoffStrategy" /> names only the growth law and this decorator adds the randomness, so
/// jitter composes over every law rather than being baked into some of them.
/// </para>
/// <para>
/// <b>Uniform over <c>[0, nominal)</c> -- "full jitter" -- and the lower bound is the point.</b> A band
/// around the nominal delay, which is what the previous implementation computed, keeps every client's
/// retries clustered near the same instant; it adds noise without decorrelating. Sampling down to zero is
/// what actually spreads a thundering herd, because two clients that failed together are no longer likely
/// to retry together. It also cannot exceed the nominal delay, so the configured maximum remains a real
/// ceiling -- which the contract on <see cref="IBackoffCalculator" /> requires and a symmetric band
/// violates on its upper half.
/// </para>
/// <para>
/// There is no jitter-factor knob. A factor only makes sense for a band, and tuning it was the mechanism
/// by which the old implementation could be configured into not decorrelating at all.
/// </para>
/// </remarks>
internal sealed class JitteredBackoffCalculator : IBackoffCalculator
{
	private readonly IBackoffCalculator _inner;
	private readonly Func<double> _jitterSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="JitteredBackoffCalculator" /> class.
	/// </summary>
	/// <param name="inner">The calculator supplying the nominal delay.</param>
	/// <param name="jitterSource">
	/// Returns a value in <c>[0, 1)</c>. Defaults to <see cref="Random.Shared" />, which is thread-safe
	/// and is the correct choice here: a retry delay is not secret, so a cryptographic generator would
	/// spend entropy and time on the retry path for no security benefit.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="inner" /> is <see langword="null" />.</exception>
	public JitteredBackoffCalculator(IBackoffCalculator inner, Func<double>? jitterSource = null)
	{
		ArgumentNullException.ThrowIfNull(inner);

		_inner = inner;
		_jitterSource = jitterSource ?? Random.Shared.NextDouble;
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		// The inner calculator validates `attempt` and clamps to the configured maximum, so the sampled
		// value inherits both: the argument check happens before anything is randomised, and the nominal
		// delay this samples below is already capped.
		var nominal = _inner.CalculateDelay(attempt);

		return TimeSpan.FromMilliseconds(nominal.TotalMilliseconds * _jitterSource());
	}
}
