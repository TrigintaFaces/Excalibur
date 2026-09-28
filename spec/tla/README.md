# Formal models (R4)

These are TLA+ models of seams whose blast radius is **catastrophic** — where a defect silently loses,
duplicates or cross-tenant-leaks consumer data and the consumer cannot detect it from outside.

A test samples the input space. A model check searches the whole modelled state space and either
establishes the property or returns a concrete trace that violates it. They are different classes of
guarantee, not two strengths of the same one, which is why the ladder has a rung for each.

## Run them

```bash
bash spec/tla/check-models.sh
```

```
exit 0   PASS     every arm gave the answer it must give
exit 1   FAIL     an arm gave the wrong answer -- a model regressed, or stopped being able to fail
exit 2   REFUSE   no toolchain, so NOTHING was measured. This is not a pass.
```

**Read the output, not just the exit code.** A PASS means each arm answered as required. It does **not**
mean a model is *adequate*, and no exit code can carry that judgement — the harness prints what its
current arms are and are not worth.

## Install the toolchain

Not a system install: a **pinned portable JDK**, so the version is reproducible, nothing on the machine's
`PATH` decides the result, and removing it is deleting one directory. No administrator rights needed.

```bash
mkdir -p ~/tools/tla && cd ~/tools/tla

# Temurin JRE 21 LTS
curl -L -o jre.zip \
  "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse"
unzip -q jre.zip

# TLA+ tools v1.8.0
curl -L -o tla2tools.jar \
  "https://github.com/tlaplus/tlaplus/releases/download/v1.8.0/tla2tools.jar"

# VERIFY PROVENANCE -- must print exactly this
sha1sum tla2tools.jar
# 2b8c20402dc740fed5b03f9d39f652e85d70b17c
```

**The hash check is not ceremony.** This release's asset has been replaced upstream at least once, so
"v1.8.0" does not identify a unique artifact. Pin the bytes.

`check-models.sh` discovers the toolchain under `~/tools/tla`; override with `TLA_JAVA` and `TLA_JAR`.

Verified working: TLC2 `2026.09.25.163503`, Temurin `21.0.12.1+1`.

## Not wired to CI, on purpose

CI has no Java runtime. Wiring this there would advertise a check that cannot run, which is the exact
defect most of this repository's tooling exists to hunt. It is a manual / pre-alpha instrument. **If Java
is added to CI, wire it in the same commit that adds it.**

## What is here

| file | what it is |
|---|---|
| `EventStoreGlobalPosition.tla` | Invariant J — committed global positions form a contiguous prefix — under four combinations of two independent allocation mechanisms |
| `_SelfTest.tla` | not a model of anything. It exists to be violated, proving the TLC invocation can report a violation at all |
| `check-models.sh` | runs every arm, checks each answer, and measures whether the shipped arm exercises concurrency |

## Writing a model here

**Four things, and the last two are the ones that get skipped.**

1. **A `SCOPE` comment naming what is modelled, what is abstracted away, and which implementation seam
   (`file:line`) the model claims to correspond to.** A proof is valid only inside what you modelled.
   Ariane 5's code was correct against a specification written for the previous trajectory.

2. **Arms that MUST fail, wired into the harness.** An invariant implied by the model's own type
   constraints discharges nothing. Prefer arms that fail for *different reasons* — if two mutations
   redden the same arm, that arm is doing two jobs and either regression masks the other.

3. **One variable per arm.** A single constant that switches two mechanisms at once means no arm isolates
   either, and the counterexample you get may be evidence about a mechanism other than the one your design
   document argues for. That happened here: the first version of `EventStoreGlobalPosition` folded
   serialisation and rollback into one flag, and its SCOPE block claimed evidence the model did not
   produce.

4. **Check the diameter against the process count.** If the diameter does not move when you add processes,
   the extra states are name permutations and **the model is not exercising concurrency between them** —
   whatever the state count says. The harness measures this and prints it.

## An AI-written model is a DRAFT

The checker has the final word. Published benchmarks put LLM TLA+ generation near 8.6% semantic
correctness, so an unchecked generated specification transfers risk into production wearing the costume of
rigor. Every model here must be checked, every arm's expected answer pinned in the harness, and every
model reviewed by someone who did not write it.

**That review is not a formality.** The first model in this directory passed its checker, passed its
non-vacuity arm, and was still rejected on review: its reachable state space turned out to be exactly what
its invariants enumerate.
