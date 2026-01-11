namespace Paradise.ECS.Test;

public class EntityLocationTests
{
    [Test]
    public async Task Invalid_IsNotValid()
    {
        var location = EntityLocation.Invalid;

        await Assert.That(location.IsValid).IsFalse();
        await Assert.That(location.ArchetypeId).IsEqualTo(-1);
        await Assert.That(location.IndexInChunk).IsEqualTo(-1);
    }

    [Test]
    public async Task ValidLocation_IsValid()
    {
        var location = new EntityLocation
        {
            Version = 1,
            ArchetypeId = 0,
            ChunkHandle = new ChunkHandle(0, 1),
            IndexInChunk = 0
        };

        await Assert.That(location.IsValid).IsTrue();
    }

    [Test]
    public async Task MatchesEntity_SameVersion_ReturnsTrue()
    {
        var entity = new Entity(5, 3);
        var location = new EntityLocation
        {
            Version = 3,
            ArchetypeId = 0,
            ChunkHandle = new ChunkHandle(0, 1),
            IndexInChunk = 0
        };

        await Assert.That(location.MatchesEntity(entity)).IsTrue();
    }

    [Test]
    public async Task MatchesEntity_DifferentVersion_ReturnsFalse()
    {
        var entity = new Entity(5, 3);
        var location = new EntityLocation
        {
            Version = 2, // Different version
            ArchetypeId = 0,
            ChunkHandle = new ChunkHandle(0, 1),
            IndexInChunk = 0
        };

        await Assert.That(location.MatchesEntity(entity)).IsFalse();
    }

    [Test]
    public async Task MatchesEntity_ZeroVersion_ReturnsFalse()
    {
        var entity = new Entity(5, 0);
        var location = new EntityLocation
        {
            Version = 0,
            ArchetypeId = 0,
            ChunkHandle = new ChunkHandle(0, 1),
            IndexInChunk = 0
        };

        // Even with matching versions, version 0 means invalid
        await Assert.That(location.MatchesEntity(entity)).IsFalse();
    }

    [Test]
    public async Task ToString_ValidLocation_ContainsInfo()
    {
        var location = new EntityLocation
        {
            Version = 2,
            ArchetypeId = 5,
            ChunkHandle = new ChunkHandle(3, 1),
            IndexInChunk = 10
        };

        var str = location.ToString();

        await Assert.That(str).Contains("5"); // ArchetypeId
        await Assert.That(str).Contains("3"); // ChunkHandle.Id
        await Assert.That(str).Contains("10"); // IndexInChunk
    }

    [Test]
    public async Task ToString_InvalidLocation_ContainsInvalid()
    {
        var str = EntityLocation.Invalid.ToString();

        await Assert.That(str).Contains("Invalid");
    }
}
