// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using System.Collections.Concurrent;

using Shouldly;

namespace Tests.Shared.Conformance;

/// <summary>
/// Shared assertion for the one window in a store's life where lazy initialisation can race.
/// </summary>
/// <remarks>
/// <para>
/// Ten MongoDB stores shipped with an unsynchronised initialisation, and the same shape was then
/// found across other providers: a flag check, then a null check, then several fields assigned one
/// after another with no lock. A second caller arriving mid-sequence observes the first field
/// already set, skips the whole block, and uses a field that is still null. 43 types under src
/// initialise lazily; the ones still unguarded are listed in unguarded-lazy-init-baseline.txt.
/// </para>
/// <para>
/// The window is a few instructions wide, which is what makes an ordinary concurrency test a poor
/// detector: the inbox store's existing one caught the defect in 2 runs out of 12, so it would pass
/// over a broken store most of the time. A release barrier helps — every caller arrives together
/// rather than staggered by task-scheduling luck — but it is not sufficient, and the caller must
/// know why.
/// </para>
/// <para>
/// THIS HELPER CANNOT SEE THE RACE AT ALL unless the store it is given is genuinely uninitialised.
/// A store that has already been initialised has passed through the window and can never re-enter
/// it, however many callers are thrown at it afterwards. That is not hypothetical: 22 of the 77
/// conformance derivers initialise inside their factory, so for nearly a third of them this
/// assertion is vacuous with respect to the race by construction. Treat it as a guard against gross
/// concurrency faults. The race is bound by LazyInitialisationRunsExactlyOnceShould, which forces
/// the interleaving deterministically instead of hoping to observe it, and by
/// LazyInitialisationIsGuardedTests, which asserts the guard exists in every store.
/// </para>
/// <para>
/// The operation under test must be a READ of something that does not exist. A read cannot corrupt a
/// shared fixture if it does run twice, and every store answers "not found" without throwing, so any
/// exception at all is the finding.
/// </para>
/// </remarks>
public static class ConcurrentFirstUse
{
    /// <summary>
    /// Drives <paramref name="firstOperation"/> from many callers at once against a freshly created
    /// store and asserts that none of them fault.
    /// </summary>
    /// <param name="firstOperation">
    /// A read against a key that does not exist. Called once per concurrent caller.
    /// </param>
    /// <param name="storeDescription">Store name, used only in the failure message.</param>
    /// <param name="callers">How many callers race. The default is generous rather than minimal.</param>
    public static async Task ShouldNotFaultAsync(
        Func<Task> firstOperation,
        string storeDescription,
        int callers = 24)
    {
        ArgumentNullException.ThrowIfNull(firstOperation);

        // RunContinuationsAsynchronously: without it the caller that completes the barrier runs the
        // first continuation inline on its own thread, which serialises the very race being tested.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var faults = new ConcurrentBag<Exception>();

        var racers = Enumerable.Range(0, callers).Select(async _ =>
        {
            await release.Task.ConfigureAwait(false);

            try
            {
                await firstOperation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                faults.Add(ex);
            }
        }).ToArray();

        release.SetResult();
        await Task.WhenAll(racers).ConfigureAwait(false);

        if (faults.IsEmpty)
        {
            return;
        }

        // A NullReferenceException is the specific signature of the initialisation race, so name it:
        // any other fault here is a different defect and should not be filed as this one.
        var nullReferences = faults.Count(f => f is NullReferenceException);
        var distinct = faults.Select(f => $"{f.GetType().Name}: {f.Message}").Distinct(StringComparer.Ordinal);

        faults.ShouldBeEmpty(
            $"{faults.Count} of {callers} concurrent first callers to {storeDescription} faulted"
            + (nullReferences > 0
                ? $", {nullReferences} with NullReferenceException — the signature of unsynchronised lazy "
                  + "initialisation, where one caller assigns the client and is still assigning the rest "
                  + "when another observes it, skips the block, and dereferences null. Serialise "
                  + "initialisation: SemaphoreSlim(1,1), a volatile flag, a re-check inside the lock, "
                  + "Release in a finally."
                : ". These are reads of keys that do not exist, so a store that is initialised correctly "
                  + "returns not-found rather than throwing; the fault is the finding.")
            + $" Distinct faults: {string.Join(" | ", distinct)}");
    }
}
