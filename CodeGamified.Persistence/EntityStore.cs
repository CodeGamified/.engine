// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Typed repository wrapper that combines IGitRepository with IEntitySerializer.
    /// Provides strongly-typed CRUD over a specific entity category.
    ///
    /// Usage:
    ///   var programs = new EntityStore&lt;Program&gt;(repo, serializer, "programs");
    ///   programs.Save("player1", "autopilot", myProgram, "updated nav logic");
    ///   Program p = programs.Load("player1", "autopilot");
    /// </summary>
    public class EntityStore<T> where T : class
    {
        readonly IGitRepository _repo;
        readonly IEntitySerializer<T> _serializer;
        readonly string _category;

        public EntityStore(IGitRepository repo, IEntitySerializer<T> serializer, string category)
        {
            _repo = repo;
            _serializer = serializer;
            _category = category;
        }

        /// <summary>Save an entity under a player's category directory.</summary>
        public GitResult Save(string playerId, string entityName, T entity, string commitMessage)
        {
            string path = GitPath.PlayerEntity(playerId, _category, entityName);
            string json = _serializer.Serialize(entity);
            return _repo.Save(path, json, commitMessage);
        }

        /// <summary>Load an entity from a player's category directory. Returns null if not found.</summary>
        public T Load(string playerId, string entityName)
        {
            string path = GitPath.PlayerEntity(playerId, _category, entityName);
            string json = _repo.Load(path);
            return json == null ? null : _serializer.Deserialize(json);
        }

        /// <summary>List all entity names for a player in this category.</summary>
        public IReadOnlyList<string> ListNames(string playerId)
        {
            string dir = $"{GitPath.PlayerDir(playerId)}/{GitPath.SanitizeName(_category)}";
            return _repo.List(dir);
        }

        /// <summary>Delete an entity from a player's category directory.</summary>
        public GitResult Delete(string playerId, string entityName, string commitMessage)
        {
            string path = GitPath.PlayerEntity(playerId, _category, entityName);
            return _repo.Delete(path, commitMessage);
        }

        /// <summary>Check if an entity exists for a player.</summary>
        public bool Exists(string playerId, string entityName)
        {
            string path = GitPath.PlayerEntity(playerId, _category, entityName);
            return _repo.Exists(path);
        }

        /// <summary>Load an entity from the shared directory.</summary>
        public T LoadShared(string entityName)
        {
            string path = GitPath.SharedEntity(_category, entityName);
            string json = _repo.Load(path);
            return json == null ? null : _serializer.Deserialize(json);
        }

        /// <summary>Save an entity to the shared directory (publish).</summary>
        public GitResult SaveShared(string entityName, T entity, string commitMessage)
        {
            string path = GitPath.SharedEntity(_category, entityName);
            string json = _serializer.Serialize(entity);
            return _repo.Save(path, json, commitMessage);
        }

        /// <summary>Get version history for a player's entity.</summary>
        public IReadOnlyList<GitCommitInfo> GetHistory(string playerId, string entityName, int maxCount = 10)
        {
            string path = GitPath.PlayerEntity(playerId, _category, entityName);
            return _repo.GetHistory(path, maxCount);
        }

        /// <summary>Load an entity at a specific historical commit.</summary>
        public T LoadAtCommit(string playerId, string entityName, string commitHash)
        {
            string path = GitPath.PlayerEntity(playerId, _category, entityName);
            string json = _repo.LoadAtCommit(path, commitHash);
            return json == null ? null : _serializer.Deserialize(json);
        }
    }
}
