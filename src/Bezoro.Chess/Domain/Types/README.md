# Domain/Types/

This directory contains foundational domain types divided into **Structs** and **Records**.

## Design Principles

- Prioritize immutable value types using readonly structs
- Use structs for small, performance-critical data structures
- Only use records when reference type semantics are necessary
- Focus on encapsulating domain concepts with strong type safety
- If you think you need a--non-static, functions only--class, you are probably doing something very wrong

## Examples

- Position (readonly struct): Represents a square on the chess board
- PieceType (enum): Defines the different types of chess pieces
- PieceColor (enum): Represents piece colors (White/Black)
