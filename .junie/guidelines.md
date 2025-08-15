# Bezoro Framework — Project Overview

This repository contains the core building blocks for the Bezoro ecosystem with a focus on chess and UCI engine interoperability.

## Solution Layout

Primary modules (under src/):
- Bezoro.Core
  - Core utilities, logging, ECS, systems, and common domain primitives used by other modules.
- Bezoro.UCI
  - UCI (Universal Chess Interface) communication layer.
  - Depends on Bezoro.Core.
- Bezoro.Chess
  - Chess-specific domain, API, and orchestration logic.
  - Depends on Bezoro.Core and Bezoro.UCI.

Additional modules present in this repo (for completeness):
- Bezoro.GameSystems (domain/game systems shared infrastructure)
- Chess.Core (legacy/alternate chess components)

## Dependencies Between Modules
- Bezoro.UCI → Bezoro.Core
- Bezoro.Chess → Bezoro.Core, Bezoro.UCI

## Tests
- Tests live under tests/ with xUnit.
- Mocking framework: NSubstitute.
- There are unit, integration, and performance-oriented tests where applicable.

## Target Frameworks
- Libraries target netstandard2.1 and/or net9.0 (depending on project).

## Repository Layout (high-level)
- src/ — production code for all modules
- tests/ — automated tests
- .junie/ — assistant configuration and guidelines (this file)
