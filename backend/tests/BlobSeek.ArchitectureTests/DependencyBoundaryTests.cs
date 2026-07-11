namespace BlobSeek.ArchitectureTests;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Domain_DoesNotReferenceInfrastructure()
    {
        var domainReferences = typeof(BlobSeek.Domain.BlobIndexState)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("BlobSeek.Infrastructure", domainReferences);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", domainReferences);
    }
}
