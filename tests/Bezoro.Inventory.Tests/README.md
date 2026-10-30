# Bezoro.Inventory.Tests

Catalog, stack, and fixed-slot inventory tests. Capacity failures must preserve all slots, transfers conserve aggregate quantities, and restore validates complete snapshots before modifying storage. Tests also cover quantities beyond Int32, catalog identity, empty values, borrowed spans, and a warmed no-allocation workload.

The differential model plans updates against copied arrays and commits complete plans. Four seeds/capacities exercise 40,000 mutation attempts, comparing every slot and occupied-slot count after each operation. This uses a different failure strategy from the runtime's preflight scans.

```sh
dotnet test tests/Bezoro.Inventory.Tests/Bezoro.Inventory.Tests.csproj -c Release
```

Tests use managed .NET on the host. They do not establish Unity/IL2CPP or Godot export compatibility.
