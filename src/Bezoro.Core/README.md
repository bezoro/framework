# Bezoro.Core

Bezoro.Core is the foundational library of the Bezoro Framework, providing core utilities, primitive types, and general-purpose abstractions.

## Key Features

- **Core Utilities**:
  - `Try`: Safe execution patterns for functions and actions.
  - `Singleton`: A robust, thread-safe, and flexible Singleton base class with support for runtime overrides and scoped testing.
  - `ResultFactory`: Utilities for creating operation results.
  - `Grid2D`: Generic 2D grid structure.
  - `StringTags`: Thread-safe manager for global string tag registration and substitution.
  - `CodeWriter` & `CSharpCodeBuilder`: Powerful utilities for generating structured, indented C# code with scope management.
- **Specialized Types**: High-performance and utility types like `UIntVector2`, `Percent`, `Color`, and `SwapbackArray`.
- **Modern C# Compatibility**: Includes polyfills for modern C# features (e.g., `RequiredMemberAttribute`, `IsExternalInit`, `CallerArgumentExpressionAttribute`) to support multiple target frameworks.

## Project Structure

- `Abstractions/`: Base interfaces and abstract definitions.
- `CodeGen/`: Specialized utilities for C# code generation.
- `Compatibility/`: Polyfills for modern C# features to support older target frameworks.
- `Extensions/`: Extensive collection of extension methods for built-in and custom types.
- `Helpers/`: Utility classes for validation, comparison, and common operations.
- `Types/`: Core framework types, primitives, and advanced data structures.
- `Utilities/`: General-purpose utilities, constants, and string management.

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`

*Necessary for compatibility with Unity.*

---
Part of the Bezoro Framework.
