// CodeGamified.Persistence — Git-backed persistence framework
// MIT License

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Serialization contract for game entities.
    /// Games implement this per entity type (programs, ships, configs).
    /// </summary>
    public interface IEntitySerializer<T>
    {
        /// <summary>Serialize an entity to JSON.</summary>
        string Serialize(T entity);

        /// <summary>Deserialize an entity from JSON.</summary>
        T Deserialize(string json);

        /// <summary>Schema version for migration support.</summary>
        int SchemaVersion { get; }
    }
}
