# Kilo

A game engine built in C# following Bevy's philosophy: everything except ECS is a plugin.

## Architecture

```
Game Code / Plugins
       |
   Kilo.ECS (Anti-Corruption Layer)
       |
   TinyEcs + TinyEcs.Bevy (internal, never exposed to game code)
```

**Kilo.ECS** wraps TinyEcs (high-performance, reflection-free ECS) with a zero-overhead anti-corruption layer. Game and plugin code uses Kilo types exclusively.

## Quick Start

```csharp
using Kilo.ECS;

var app = new KiloApp();

// Spawn entities at startup
app.AddSystem(KiloStage.Startup, world =>
{
    world.Entity()
        .Set(new Position { X = 0, Y = 0 })
        .Set(new Velocity { Dx = 1, Dy = 0.5f });
});

// Update logic
app.AddSystem(KiloStage.Update, world =>
{
    var query = world.QueryBuilder()
        .With<Position>()
        .With<Velocity>()
        .Build();

    var iter = query.Iter();
    while (iter.Next())
    {
        var positions = iter.Data<Position>(0);
        var velocities = iter.Data<Velocity>(1);
        for (int i = 0; i < iter.Count; i++)
        {
            positions[i].X += velocities[i].Dx * 0.016f;
            positions[i].Y += velocities[i].Dy * 0.016f;
        }
    }
});

// Game loop
while (running)
    app.Run();

struct Position { public float X, Y; }
struct Velocity { public float Dx, Dy; }
```

## Core Types

| Kilo Type | Description |
|---|---|
| `KiloWorld` | Main ECS container (wraps TinyEcs.World) |
| `KiloEntity` | Entity handle with fluent API (ref struct) |
| `EntityId` | Entity identifier (ulong wrapper) |
| `Ptr<T>` | Mutable component pointer (ref struct) |
| `KiloQuery` | Cached entity query |
| `KiloQueryBuilder` | Query builder with filters |
| `KiloApp` | Application framework with stages |
| `KiloStage` | Execution stage (Startup, Update, etc.) |
| `IKiloPlugin` | Plugin interface |

## Anti-Corruption Layer

All wrapper types use `[MethodImpl(AggressiveInlining)]` for zero-overhead forwarding. The JIT produces identical machine code to calling TinyEcs directly.

- **Class wrappers** (KiloWorld, KiloApp): Store internal reference, forward all calls
- **Ref struct wrappers** (KiloEntity, Ptr<T>): Store underlying value, forward all methods
- **No boxing, no allocations** in hot paths

## Plugin System

Following Bevy's philosophy, everything besides ECS is a plugin:

```csharp
struct PhysicsPlugin : IKiloPlugin
{
    public void Build(KiloApp app)
    {
        app.AddSystem(KiloStage.Update, world => ApplyGravity(world));
        app.AddSystem(KiloStage.PostUpdate, world => ResolveCollisions(world));
    }
}

app.AddPlugin(new PhysicsPlugin { Gravity = 9.81f });
```

## Stages

Systems execute in stages (Bevy-inspired):

1. `KiloStage.Startup` - Runs once (single-threaded)
2. `KiloStage.First`
3. `KiloStage.PreUpdate`
4. `KiloStage.Update` - Main gameplay
5. `KiloStage.PostUpdate`
6. `KiloStage.Last` - Rendering, cleanup

Custom stages:
```csharp
var physics = KiloStage.Custom("Physics");
app.AddStage(physics).After(KiloStage.Update).Before(KiloStage.PostUpdate).Build();
```

## Requirements

- .NET 9.0 SDK (for building)
- .NET 9.0 Runtime (for running)

## Building

```bash
dotnet build Kilo.slnx
```

## Testing

```bash
dotnet test tests/Kilo.ECS.Tests/
```

## Project Structure

```
kilo/
├── Kilo.slnx
├── Directory.Build.props
├── src/
│   └── Kilo.ECS/           # Anti-corruption layer
│       ├── Primitives/     # EntityId, Ptr<T>, ComponentInfo
│       ├── World/          # KiloWorld, KiloEntity
│       ├── Query/          # KiloQueryBuilder, KiloQuery, KiloQueryIterator
│       ├── App/            # KiloApp, KiloStage, IKiloPlugin, ThreadingMode
│       ├── SystemParams/   # Future: Commands, Res, ResMut, etc.
│       ├── Observers/      # Trigger interfaces
│       ├── State/          # State<T>, NextState<T>
│       └── Bundles/        # IKiloBundle
├── tests/
│   └── Kilo.ECS.Tests/    # xUnit tests
├── samples/
│   └── Kilo.Samples.HelloECS/
└── README.md
```

## Roadmap

### Phase 1 (Current): ECS Anti-Corruption Layer
- [x] Core ECS wrapping (KiloWorld, KiloEntity, EntityId)
- [x] Query system (KiloQueryBuilder, KiloQuery)
- [x] App framework (KiloApp, KiloStage, IKiloPlugin)
- [x] State management wrappers
- [x] Bundle interface
- [x] Unit tests
- [x] HelloECS sample

### Phase 2: Documentation & Research
- [ ] Veldrid rendering guide (comprehensive)
- [ ] BepuPhysics guide (comprehensive)
- [ ] Plugin selection reports (audio, input, assets)
- [ ] Editor architecture analysis report (including R3 ViewModel evaluation)

### Phase 3: Plugin Development
- [ ] Kilo.Rendering (Veldrid)
- [ ] Kilo.Physics (BepuPhysics)
- [ ] Kilo.Input
- [ ] Kilo.Assets

### Phase 4: Engine MVP
- [ ] Working game combining all plugins
- [ ] Performance benchmarks

### Future Directions
- [ ] Full system parameter injection (T4-generated AddSystem overloads)
- [ ] Editor plugin
- [ ] Asset pipeline
- [ ] Scene management
- [ ] Networking
- [ ] Scripting system
