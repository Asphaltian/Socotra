namespace Socotra.Vulkan.Tests;

public sealed class GpuFactAttribute : FactAttribute
{
    public GpuFactAttribute()
    {
        if (!GpuHost.Available && Environment.GetEnvironmentVariable("SOCOTRA_REQUIRE_GPU") != "1")
        {
            Skip = "No GPU here runs Vulkan 1.2 with the renderer's features. Set SOCOTRA_REQUIRE_GPU=1 to fail instead.";
        }
    }
}

public sealed class GpuTheoryAttribute : TheoryAttribute
{
    public GpuTheoryAttribute()
    {
        if (!GpuHost.Available && Environment.GetEnvironmentVariable("SOCOTRA_REQUIRE_GPU") != "1")
        {
            Skip = "No GPU here runs Vulkan 1.2 with the renderer's features. Set SOCOTRA_REQUIRE_GPU=1 to fail instead.";
        }
    }
}

[CollectionDefinition(Name)]
public sealed class GpuCollection : ICollectionFixture<GpuHost>
{
    public const string Name = "GPU";
}
