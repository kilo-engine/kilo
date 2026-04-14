using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics;

namespace Kilo.Physics;

/// <summary>
/// Narrow phase callbacks for Bepu physics.
/// Handles collision detection configuration.
/// </summary>
public struct KiloNarrowPhaseCallbacks : INarrowPhaseCallbacks
{
    /// <summary>Initialize callbacks for the simulation.</summary>
    public void Initialize(Simulation simulation)
    {
    }

    /// <summary>Choose whether to allow contact generation to proceed.</summary>
    public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin)
    {
        return true;
    }

    /// <summary>Choose whether to allow contact generation for compound collidables.</summary>
    public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
    {
        return true;
    }

    /// <summary>Configure a generic contact manifold.</summary>
    public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold>
    {
        pairMaterial = new PairMaterialProperties(0.5f, 2f, default);
        return true;
    }

    /// <summary>Configure a convex contact manifold for compound collidables.</summary>
    public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
    {
        return true;
    }

    /// <summary>Dispose callback resources.</summary>
    public void Dispose()
    {
    }
}
