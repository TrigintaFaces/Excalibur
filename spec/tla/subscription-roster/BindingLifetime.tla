------------------------- MODULE BindingLifetime -------------------------
EXTENDS Integers, TLC

(* SCOPE: finite safety model of the current member lifetime and session-read
   witness protocol. Two keys, at most two unique binding epochs per key, one
   paused checkpoint writer, one projection reader, and independent lifecycle
   and fold actions. InitializeZero is atomic background initialization, not a
   separately paused zero writer; paused Advance tests the shared epoch fence at 1.
   Target is captured (0 or 1), not a moving stream head.
   A receipt describes the producer's observed roster, not the roster at return.
   The consumer obtains ITS OWN witness using the save position, reads data,
   then validates; it does not trust the producer receipt as a current roster.

   CORRESPONDENCE: BindingId + expected-position AdvanceCheckpointAsync;
   one-row member/cursor release; SubscriptionRoster.Identity includes epochs;
   ProjectionSessionReader AwaitWitnessAsync / data read / post-read validation.
   CaptureReceipt abstracts the host's final progress observation and IssueReceipt
   preserves that observation even when the roster changes before return.

   ASSUMPTIONS: binding identities never repeat; provider conditional row mutation
   is atomic; observed member/position pairs are coherent; a stable binding's
   materialized prefix never regresses after checkpoint acknowledgement. Logical
   roster observations are abstracted as vectors. SQL isolation/query mechanics,
   partial result enumeration, hash collisions, destination writes, tenant routing,
   generation-pointer cutover, crash durability, and liveness are NOT proved here.
   Real-engine conformance and separate read/cutover tests support those seams.
   InvRead concerns the validation observation, not arbitrary later return time.
   Mutation flags are independent; each has a required counterexample.
*)
CONSTANTS FenceAdvance, FenceRelease, SnapshotMode, AbsenceAsZero, Target
Keys == {"a", "b"}
Empty == [k \in Keys |-> 0]
Absent == [k \in Keys |-> -1]
Bound(v) == {k \in Keys : v[k] # 0}
Acknowledged(p) == IF AbsenceAsZero /\ p = -1 THEN 0 ELSE p

VARIABLES binding, epoch, position, writerPhase, workKey, workEpoch, expected,
          readerPhase, witness, readValue, producerPhase, producerEpochs,
          producerPositions, badAdvance, badRead, badReceipt, accepted, issued,
          rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound
vars == <<binding, epoch, position, writerPhase, workKey, workEpoch, expected,
          readerPhase, witness, readValue, producerPhase, producerEpochs,
          producerPositions, badAdvance, badRead, badReceipt, accepted, issued,
          rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

Init == /\ binding = Empty /\ epoch = Empty /\ position = Absent
        /\ writerPhase = 0 /\ workKey = "a" /\ workEpoch = 0 /\ expected = -1
        /\ readerPhase = 0 /\ witness = Empty /\ readValue = -1
        /\ producerPhase = 0 /\ producerEpochs = Empty /\ producerPositions = Absent
        /\ badAdvance = FALSE /\ badRead = FALSE /\ badReceipt = FALSE
        /\ accepted = FALSE /\ issued = FALSE /\ rebound = FALSE /\ staleAttempt = FALSE
        /\ releasePhase = 0 /\ releaseKey = "a" /\ releaseEpoch = 0 /\ badRelease = FALSE
        /\ initializedZero = FALSE /\ badRebindAdvance = FALSE /\ staleRebound = FALSE

Bind(k) == /\ binding[k] = 0 /\ epoch[k] < 2
           /\ binding' = [binding EXCEPT ![k] = epoch[k] + 1]
           /\ epoch' = [epoch EXCEPT ![k] = @ + 1]
           /\ rebound' = (rebound \/ epoch[k] = 1)
           /\ UNCHANGED <<position, writerPhase, workKey, workEpoch, expected,
                readerPhase, witness, readValue, producerPhase, producerEpochs,
                producerPositions, badAdvance, badRead, badReceipt, accepted, issued, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

Release(k) == /\ binding[k] # 0
              /\ binding' = [binding EXCEPT ![k] = 0]
              /\ position' = [position EXCEPT ![k] = -1]
              /\ UNCHANGED <<epoch, writerPhase, workKey, workEpoch, expected,
                   readerPhase, witness, readValue, producerPhase, producerEpochs,
                   producerPositions, badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

Fold(k) == /\ binding[k] # 0 /\ position[k] < 1
           /\ position' = [position EXCEPT ![k] = 1]
           /\ UNCHANGED <<binding, epoch, writerPhase, workKey, workEpoch, expected,
                readerPhase, witness, readValue, producerPhase, producerEpochs,
                producerPositions, badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

CaptureWriter(k) == /\ writerPhase = 0 /\ binding[k] # 0
                   /\ writerPhase' = 1 /\ workKey' = k
                   /\ workEpoch' = binding[k] /\ expected' = position[k]
                   /\ UNCHANGED <<binding, epoch, position, readerPhase, witness, readValue,
                        producerPhase, producerEpochs, producerPositions, badAdvance,
                        badRead, badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

Advance == /\ writerPhase = 1
           /\ LET apply == position[workKey] = expected
                           /\ (~FenceAdvance \/ binding[workKey] = workEpoch)
              IN /\ position' = IF apply THEN [position EXCEPT ![workKey] = 1] ELSE position
                 /\ badAdvance' = (badAdvance \/ (apply /\ binding[workKey] # workEpoch))
                 /\ badRebindAdvance' = (badRebindAdvance \/ (apply /\ binding[workKey] # 0 /\ binding[workKey] # workEpoch))
           /\ staleAttempt' = (staleAttempt \/ binding[workKey] # workEpoch)
           /\ staleRebound' = (staleRebound \/ (binding[workKey] # 0 /\ binding[workKey] # workEpoch))
           /\ writerPhase' = 2
           /\ UNCHANGED <<binding, epoch, workKey, workEpoch, expected, readerPhase,
                witness, readValue, producerPhase, producerEpochs, producerPositions,
                badRead, badReceipt, accepted, issued, rebound, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero>>

Observe == /\ readerPhase = 0 /\ binding["a"] # 0 /\ position["a"] >= Target
           /\ witness' = binding /\ readerPhase' = 1
           /\ UNCHANGED <<binding, epoch, position, writerPhase, workKey, workEpoch,
                expected, readValue, producerPhase, producerEpochs, producerPositions,
                badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

ReadData == /\ readerPhase = 1 /\ readValue' = position["a"] /\ readerPhase' = 2
            /\ UNCHANGED <<binding, epoch, position, writerPhase, workKey, workEpoch,
                 expected, witness, producerPhase, producerEpochs, producerPositions,
                 badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

SnapshotMatches == CASE SnapshotMode = "epochs" -> witness = binding
                    [] SnapshotMode = "keys" -> Bound(witness) = Bound(binding)
                    [] OTHER -> TRUE
ValidateRead == /\ readerPhase = 2
                /\ LET fresh == SnapshotMatches /\ witness["a"] = binding["a"]
                                /\ position["a"] >= Target
                   IN /\ accepted' = fresh
                      /\ badRead' = (fresh /\ (witness # binding \/ readValue < Target))
                /\ readerPhase' = 3
                /\ UNCHANGED <<binding, epoch, position, writerPhase, workKey, workEpoch,
                     expected, witness, readValue, producerPhase, producerEpochs,
                     producerPositions, badAdvance, badReceipt, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

CaptureReceipt == /\ producerPhase = 0 /\ Bound(binding) # {}
                  /\ producerEpochs' = binding /\ producerPositions' = position
                  /\ producerPhase' = 1
                  /\ UNCHANGED <<binding, epoch, position, writerPhase, workKey, workEpoch,
                       expected, readerPhase, witness, readValue, badAdvance, badRead,
                       badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>

IssueReceipt == /\ producerPhase = 1
                /\ \A k \in Bound(producerEpochs) : Acknowledged(producerPositions[k]) >= Target
                /\ issued' = TRUE /\ producerPhase' = 2
                /\ badReceipt' = (\E k \in Bound(producerEpochs) : producerPositions[k] < Target)
                /\ UNCHANGED <<binding, epoch, position, writerPhase, workKey, workEpoch,
                     expected, readerPhase, witness, readValue, producerEpochs,
                     producerPositions, badAdvance, badRead, accepted, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, initializedZero, badRebindAdvance, staleRebound>>


InitializeZero(k) == /\ binding[k] # 0 /\ position[k] = -1
                    /\ position' = [position EXCEPT ![k] = 0]
                    /\ initializedZero' = TRUE
 /\ UNCHANGED <<binding, epoch, writerPhase, workKey, workEpoch, expected, readerPhase, witness, readValue, producerPhase, producerEpochs, producerPositions, badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, releasePhase, releaseKey, releaseEpoch, badRelease, badRebindAdvance, staleRebound>>

CaptureRelease(k) == /\ releasePhase = 0 /\ binding[k] # 0
                     /\ releasePhase' = 1 /\ releaseKey' = k /\ releaseEpoch' = binding[k]
 /\ UNCHANGED <<binding, epoch, position, writerPhase, workKey, workEpoch, expected, readerPhase, witness, readValue, producerPhase, producerEpochs, producerPositions, badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, badRelease, initializedZero, badRebindAdvance, staleRebound>>

FinishRelease == /\ releasePhase = 1
                 /\ LET apply == binding[releaseKey] # 0
                                  /\ (~FenceRelease \/ binding[releaseKey] = releaseEpoch)
                     IN /\ binding' = IF apply THEN [binding EXCEPT ![releaseKey] = 0] ELSE binding
                        /\ position' = IF apply THEN [position EXCEPT ![releaseKey] = -1] ELSE position
                        /\ badRelease' = (badRelease \/ (apply /\ binding[releaseKey] # releaseEpoch))
                 /\ releasePhase' = 2
 /\ UNCHANGED <<epoch, writerPhase, workKey, workEpoch, expected, readerPhase, witness, readValue, producerPhase, producerEpochs, producerPositions, badAdvance, badRead, badReceipt, accepted, issued, rebound, staleAttempt, releaseKey, releaseEpoch, initializedZero, badRebindAdvance, staleRebound>>

Next == (\E k \in Keys : Bind(k) \/ Release(k) \/ Fold(k) \/ CaptureWriter(k) \/ InitializeZero(k) \/ CaptureRelease(k))
        \/ FinishRelease \/ Advance \/ Observe \/ ReadData \/ ValidateRead \/ CaptureReceipt \/ IssueReceipt
Spec == Init /\ [][Next]_vars
InvEpoch == ~badAdvance
InvRelease == ~badRelease
InvRebindFence == ~badRebindAdvance
NeverZero == ~initializedZero
NeverStaleRebound == ~staleRebound
InvCheckpoint == \A k \in Keys : binding[k] = 0 => position[k] = -1
InvRead == ~badRead
InvReceipt == ~badReceipt
NeverAccepted == ~accepted
NeverIssued == ~issued
NeverRebound == ~rebound
NeverStaleAttempt == ~staleAttempt
=============================================================================
