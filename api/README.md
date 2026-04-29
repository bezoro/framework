# Public API Baselines

These files are the pre-1.0 public type baseline for the framework packages under `src/`.

Run this after intentional public type changes:

```powershell
./scripts/Export-PublicApi.ps1
```

CI checks the baseline with:

```powershell
./scripts/Export-PublicApi.ps1 -Check
```

Review flags are intentionally conservative:

- `review-unsealed-class`: public class inheritance is part of the contract unless the type is sealed.
- `review-mutable-struct`: public value type has mutable shape and should be reviewed for copy semantics.
- `review-mutable-record-struct`: public record struct has mutable shape unless declared readonly.
- `review-contract`: public interface or abstract type has explicit implementation/versioning cost.
- `review-naming`: public type name does not match the expected PascalCase type naming shape.
- `stable-shape`: no type-shape concern was detected by the baseline exporter.
