using Kilo.Rendering.Driver;
using Kilo.Rendering.Meshes;
using Xunit;

namespace Kilo.Rendering.Tests;

public class SkinnedMeshTests
{
    [Fact]
    public void SkinnedMesh_Layout_HasCorrectStride()
    {
        Assert.Equal(80u, SkinnedMesh.Layout.ArrayStride);
    }

    [Fact]
    public void SkinnedMesh_Layout_Has6Attributes()
    {
        Assert.Equal(6, SkinnedMesh.Layout.Attributes.Length);
    }

    [Fact]
    public void SkinnedMesh_Layout_AttributesHaveCorrectLocations()
    {
        var layout = SkinnedMesh.Layout;

        Assert.Equal(0, layout.Attributes[0].ShaderLocation);
        Assert.Equal(1, layout.Attributes[1].ShaderLocation);
        Assert.Equal(2, layout.Attributes[2].ShaderLocation);
        Assert.Equal(3, layout.Attributes[3].ShaderLocation);
        Assert.Equal(4, layout.Attributes[4].ShaderLocation);
        Assert.Equal(5, layout.Attributes[5].ShaderLocation);
    }

    [Fact]
    public void SkinnedMesh_Layout_AttributesHaveCorrectFormats()
    {
        var layout = SkinnedMesh.Layout;

        Assert.Equal(VertexFormat.Float32x3, layout.Attributes[0].Format);
        Assert.Equal(VertexFormat.Float32x3, layout.Attributes[1].Format);
        Assert.Equal(VertexFormat.Float32x2, layout.Attributes[2].Format);
        Assert.Equal(VertexFormat.Float32x4, layout.Attributes[3].Format);
        Assert.Equal(VertexFormat.UInt32x4, layout.Attributes[4].Format);
        Assert.Equal(VertexFormat.Float32x4, layout.Attributes[5].Format);
    }

    [Fact]
    public void SkinnedMesh_Layout_AttributesHaveCorrectOffsets()
    {
        var layout = SkinnedMesh.Layout;

        Assert.Equal(0u, layout.Attributes[0].Offset);
        Assert.Equal(12u, layout.Attributes[1].Offset);
        Assert.Equal(24u, layout.Attributes[2].Offset);
        Assert.Equal(32u, layout.Attributes[3].Offset);
        Assert.Equal(48u, layout.Attributes[4].Offset);
        Assert.Equal(64u, layout.Attributes[5].Offset);
    }
}
