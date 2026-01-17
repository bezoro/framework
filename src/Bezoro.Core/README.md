# Bezoro.Core

Bezoro.Core is the foundational library of the Bezoro Framework, providing core utilities, primitive types, and general-purpose abstractions.

## Key Features

- **Common Utilities**:
  - `Try`: Safe execution patterns for functions and actions.
  - `Singleton`: A robust, thread-safe, and flexible Singleton base class with support for runtime overrides and scoped testing.
  - `ResultFactory`: Utilities for creating operation results.
  - `Grid2D`: Generic 2D grid structure.
  - `CodeWriter` & `CSharpCodeBuilder`: Powerful utilities for generating structured, indented C# code with scope management.
- **Primitives**: Specialized types like `UIntVector2`, `Percent`, `Color`, and `SwapbackArray`.
- **Modern C# Compatibility**: Includes polyfills for modern C# features (e.g., `RequiredMemberAttribute`, `IsExternalInit`, `CallerArgumentExpressionAttribute`) to support multiple target frameworks.

## Project Structure

- `CodeGen`: Code generation utilities.


- `Common/`: Base interfaces, helpers, and extension methods.
  - `/Extensions/`: Extension methods for common types.
  - `/Helpers/`: Helper classes and utilities.
  - `/Interfaces/`: Base interfaces.
  - `/Primitives/`: Basic data structures and specialized types.


- `Grid/`: Generic 2D and 3D grid structure and related utilities.

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`

*Necessary for compatibility with Unity.*

---
Part of the Bezoro Framework.
