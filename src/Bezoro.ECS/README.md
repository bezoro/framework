# Bezoro.ECS

Bezoro.ECS is a lightweight and efficient Entity Component System (ECS) implementation, part of the Bezoro Framework.

## Key Features

- **Entity Management**: Fast creation and destruction of entities with ID recycling.
- **Component Management**: Type-safe storage and retrieval of components using structs for performance.
- **System Management**: Organized registration and execution of logic systems.
- **Minimal Dependencies**: Designed to be lightweight and easy to integrate.

## Getting Started

The ECS implementation is built around the `World` class, which coordinates entities, components, and systems.

### Example Usage

```csharp
using Bezoro.ECS;

// Create a new world
var world = new World();

// Create an entity
var entity = world.CreateEntity();

// Define a component
public struct Position : IComponent 
{
    public float X;
    public float Y;
}

// Add component to entity
world.AddComponent(entity, new Position { X = 10, Y = 20 });

// Query component
if (world.HasComponent<Position>(entity))
{
    var pos = world.GetComponent<Position>(entity);
}

// Define a system
public class MovementSystem : ISystem
{
    private readonly World _world;
    public MovementSystem(World world) => _world = world;
    
    public void Update() 
    {
        // System logic here
    }
}

// Register and update systems
world.RegisterSystem(new MovementSystem(world));
world.Update();
```

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`

---
Part of the Bezoro Framework.
