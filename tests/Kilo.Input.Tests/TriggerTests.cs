using Kilo.Input.Triggers;
using Xunit;

namespace Kilo.Input.Tests;

public class TriggerTests
{
    [Fact]
    public void PressTrigger_Active_ReturnsTriggered()
    {
        var trigger = new PressTrigger();
        Assert.Equal(TriggerState.Triggered, trigger.Update(1.0f, 0.016f));
    }

    [Fact]
    public void PressTrigger_Inactive_ReturnsNone()
    {
        var trigger = new PressTrigger();
        Assert.Equal(TriggerState.None, trigger.Update(0.0f, 0.016f));
    }

    [Fact]
    public void HoldTrigger_NotHeldLongEnough_ReturnsOngoing()
    {
        var trigger = new HoldTrigger { Duration = 0.5f };
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.1f));
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.2f));
    }

    [Fact]
    public void HoldTrigger_HeldLongEnough_ReturnsTriggered()
    {
        var trigger = new HoldTrigger { Duration = 0.3f };
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.1f));
        Assert.Equal(TriggerState.Triggered, trigger.Update(1.0f, 0.25f));
    }

    [Fact]
    public void HoldTrigger_Released_Resets()
    {
        var trigger = new HoldTrigger { Duration = 0.3f };
        trigger.Update(1.0f, 0.2f); // ongoing
        Assert.Equal(TriggerState.None, trigger.Update(0.0f, 0.016f)); // released → reset
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.1f)); // start over
    }

    [Fact]
    public void PulseTrigger_FiresAtInterval()
    {
        var trigger = new PulseTrigger { Interval = 0.1f };

        // Accumulate to just before first fire
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.05f));
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.04f));

        // Cross the interval → fires
        Assert.Equal(TriggerState.Triggered, trigger.Update(1.0f, 0.02f));

        // Reset after fire, accumulate again
        Assert.Equal(TriggerState.Ongoing, trigger.Update(1.0f, 0.05f));
    }

    [Fact]
    public void PulseTrigger_Released_Resets()
    {
        var trigger = new PulseTrigger { Interval = 0.1f };
        trigger.Update(1.0f, 0.08f);
        Assert.Equal(TriggerState.None, trigger.Update(0.0f, 0.016f));
    }
}
