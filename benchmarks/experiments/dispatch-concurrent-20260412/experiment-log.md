# Auto-Optimize: Dispatch Concurrent Performance (Round 5)

## Goal
Minimize Mean for 'Dispatch: 10 concurrent commands' AND 'Dispatch: 100 concurrent commands'.

## Baseline
| Method | Mean | StdDev | Allocated |
|--------|------|--------|-----------|
| Dispatch: 10 concurrent | 616.76 ns | 3.162 ns | 1360 B |
| MediatR: 10 concurrent | 551.49 ns | 5.854 ns | 1856 B |
| Dispatch: 100 concurrent | 5119.83 ns | 51.045 ns | 12160 B |
| MediatR: 100 concurrent | 5137.60 ns | 35.064 ns | 17064 B |

## Strategy
Focus areas from user:
A. MessageContext allocation in concurrent scenarios - ThreadStatic only caches 1
B. Task.FromResult allocations when dispatch completes synchronously
C. ThreadStatic ambient context Push/Pop in concurrent Task.WhenAll scenarios
D. Per-dispatch overhead scaling / lock contention

## Experiments

