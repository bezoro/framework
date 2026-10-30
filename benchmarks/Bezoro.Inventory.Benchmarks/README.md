# Inventory benchmarks

Run the Release benchmark project with preallocated catalogs, inventories, and snapshot arrays:

```sh
dotnet run -c Release --project benchmarks/Bezoro.Inventory.Benchmarks -- --filter '*InventoryBenchmarks*'
```

Workloads cover 16 and 128 slots. Counts scan the current inventory; add/remove and transfer workloads return to their original quantity after each invocation. Restore reads one preallocated complete snapshot. `AddRemovePair` measures two operations, and `TransferRoundTrip` measures two transfers. Initialization and application serialization are outside the measured workloads.

No latency result is implied by defining a benchmark. Record hardware, runtime, source revision, and raw measurement error before using results to choose a frame budget.
