using BepuPhysics;
using BepuUtilities;
using System.Numerics;

namespace Kilo.Physics;

/// <summary>
/// Pose integration callbacks for Bepu physics.
/// Handles gravity application and pose updates.
/// </summary>
public struct KiloPoseIntegratorCallbacks : IPoseIntegratorCallbacks
{
    /// <summary>Angular integration mode.</summary>
    public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.ConserveMomentum;

    /// <summary>Whether to allow substeps for unconstrained bodies.</summary>
    public bool AllowSubstepsForUnconstrainedBodies => false;

    /// <summary>Whether to integrate velocity for kinematic bodies.</summary>
    public bool IntegrateVelocityForKinematics => false;

    /// <summary>Get or set gravity vector.</summary>
    public Vector3 Gravity;

    /// <summary>Wide gravity for SIMD operations.</summary>
    private Vector3Wide _gravityWide;

    /// <summary>Initialize callbacks for the simulation.</summary>
    public void Initialize(Simulation simulation)
    {
    }

    /// <summary>Prepare for integration phase.</summary>
    public void PrepareForIntegration(float dt)
    {
        // Precompute wide gravity for SIMD operations
        _gravityWide.X = new Vector<float>(Gravity.X);
        _gravityWide.Y = new Vector<float>(Gravity.Y);
        _gravityWide.Z = new Vector<float>(Gravity.Z);
    }

    /// <summary>Apply forces and velocities to a bundle of bodies.</summary>
    public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity)
    {
        // Apply gravity to linear velocity for each active lane
        velocity.Linear.X += _gravityWide.X * dt;
        velocity.Linear.Y += _gravityWide.Y * dt;
        velocity.Linear.Z += _gravityWide.Z * dt;
    }

    /// <summary>Create callbacks with the given gravity.</summary>
    public KiloPoseIntegratorCallbacks(Vector3 gravity)
    {
        Gravity = gravity;
        _gravityWide = default;
    }
}
