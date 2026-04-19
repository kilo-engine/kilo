using Kilo.Window;
using Xunit;

namespace Kilo.Window.Tests;

public class ComponentTests
{
    [Fact]
    public void InputReceiver_IsStruct()
    {
        // Verify InputReceiver is a value type (struct)
        var type = typeof(InputReceiver);
        Assert.True(type.IsValueType);
    }

    [Fact]
    public void InputReceiver_DefaultConstruction()
    {
        // InputReceiver should be default-constructible as it's an empty struct
        var receiver = new InputReceiver();
        Assert.IsType<InputReceiver>(receiver);
    }
}
