// ═══════════════════════════════════════════════════════════
//  PersistenceTests — Unit tests for GitDB persistence
//  Run via Unity Test Runner (Edit Mode)
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using NUnit.Framework;
using CodeGamified.Persistence;
using CodeGamified.Persistence.Providers;

namespace CodeGamified.Persistence.Tests
{
    public class PersistenceTests
    {
        MemoryGitProvider _repo;

        [SetUp]
        public void SetUp()
        {
            _repo = new MemoryGitProvider();
        }

        // ── GitPath ─────────────────────────────────────────────

        [Test] public void GitPath_SanitizeId_AllowsAlphanumHyphenUnderscore()
        {
            Assert.AreEqual("player-1_ok", GitPath.SanitizeId("player-1_ok"));
        }

        [Test] public void GitPath_SanitizeId_ReplacesSpecialChars()
        {
            Assert.AreEqual("a_b_c", GitPath.SanitizeId("a/b\\c"));
        }

        [Test] public void GitPath_SanitizeId_EmptyReturnsPlaceholder()
        {
            Assert.AreEqual("_empty", GitPath.SanitizeId(""));
            Assert.AreEqual("_empty", GitPath.SanitizeId(null));
        }

        [Test] public void GitPath_SanitizeName_AllowsDots()
        {
            Assert.AreEqual("auto.pilot", GitPath.SanitizeName("auto.pilot"));
        }

        [Test] public void GitPath_PlayerProgram_BuildsCorrectPath()
        {
            string path = GitPath.PlayerProgram("alice", "autopilot");
            Assert.AreEqual("players/alice/programs/autopilot.json", path);
        }

        [Test] public void GitPath_SharedProgram_BuildsCorrectPath()
        {
            string path = GitPath.SharedProgram("nav-utils");
            Assert.AreEqual("shared/programs/nav-utils.json", path);
        }

        [Test] public void GitPath_PlayerEntity_CustomCategory()
        {
            string path = GitPath.PlayerEntity("bob", "ships", "frigate");
            Assert.AreEqual("players/bob/ships/frigate.json", path);
        }

        [Test] public void GitPath_PlayerConfig_BuildsCorrectPath()
        {
            string path = GitPath.PlayerConfig("alice");
            Assert.AreEqual("players/alice/config.json", path);
        }

        // ── MemoryGitProvider CRUD ──────────────────────────────

        [Test] public void Save_And_Load_RoundTrip()
        {
            var result = _repo.Save("test/file.json", "{\"x\":1}", "init");
            Assert.IsTrue(result.Success);
            Assert.AreEqual("{\"x\":1}", _repo.Load("test/file.json"));
        }

        [Test] public void Load_NonExistent_ReturnsNull()
        {
            Assert.IsNull(_repo.Load("does/not/exist.json"));
        }

        [Test] public void Save_Overwrites_ExistingFile()
        {
            _repo.Save("f.json", "v1", "first");
            _repo.Save("f.json", "v2", "second");
            Assert.AreEqual("v2", _repo.Load("f.json"));
            Assert.AreEqual(2, _repo.CommitCount);
        }

        [Test] public void Delete_RemovesFile()
        {
            _repo.Save("f.json", "{}", "add");
            Assert.IsTrue(_repo.Exists("f.json"));

            var result = _repo.Delete("f.json", "remove");
            Assert.IsTrue(result.Success);
            Assert.IsFalse(_repo.Exists("f.json"));
        }

        [Test] public void Delete_NonExistent_Fails()
        {
            var result = _repo.Delete("nope.json", "rm");
            Assert.IsFalse(result.Success);
        }

        [Test] public void List_ReturnsFilesUnderPrefix()
        {
            _repo.Save("players/a/programs/p1.json", "{}", "c1");
            _repo.Save("players/a/programs/p2.json", "{}", "c2");
            _repo.Save("players/a/ships/s1.json", "{}", "c3");
            _repo.Save("players/b/programs/p3.json", "{}", "c4");

            var listed = _repo.List("players/a/programs");
            Assert.AreEqual(2, listed.Count);
            Assert.IsTrue(listed.Contains("players/a/programs/p1.json"));
            Assert.IsTrue(listed.Contains("players/a/programs/p2.json"));
        }

        [Test] public void Exists_TrueForSavedFile()
        {
            _repo.Save("x.json", "{}", "add");
            Assert.IsTrue(_repo.Exists("x.json"));
            Assert.IsFalse(_repo.Exists("y.json"));
        }

        [Test] public void GetHistory_ReturnsCommitsForPath()
        {
            _repo.Save("f.json", "v1", "first");
            _repo.Save("f.json", "v2", "second");
            _repo.Save("f.json", "v3", "third");

            var history = _repo.GetHistory("f.json", 2);
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual("third", history[0].Message);
            Assert.AreEqual("second", history[1].Message);
        }

        [Test] public void Clear_ResetsEverything()
        {
            _repo.Save("f.json", "{}", "c");
            Assert.AreEqual(1, _repo.FileCount);

            _repo.Clear();
            Assert.AreEqual(0, _repo.FileCount);
            Assert.AreEqual(0, _repo.CommitCount);
        }

        // ── EntityStore ─────────────────────────────────────────

        class TestEntity
        {
            public string Name;
            public int Score;
        }

        class TestSerializer : IEntitySerializer<TestEntity>
        {
            public int SchemaVersion => 1;

            public string Serialize(TestEntity entity) =>
                $"{{\"name\":\"{entity.Name}\",\"score\":{entity.Score}}}";

            public TestEntity Deserialize(string json)
            {
                // Minimal parse for testing
                var e = new TestEntity();
                int ni = json.IndexOf("\"name\":\"") + 8;
                int ne = json.IndexOf("\"", ni);
                e.Name = json.Substring(ni, ne - ni);
                int si = json.IndexOf("\"score\":") + 8;
                int se = json.IndexOf("}", si);
                e.Score = int.Parse(json.Substring(si, se - si));
                return e;
            }
        }

        [Test] public void EntityStore_SaveAndLoad_RoundTrip()
        {
            var store = new EntityStore<TestEntity>(_repo, new TestSerializer(), "scores");
            var entity = new TestEntity { Name = "Alice", Score = 42 };

            var result = store.Save("alice", "run1", entity, "saved score");
            Assert.IsTrue(result.Success);

            var loaded = store.Load("alice", "run1");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Alice", loaded.Name);
            Assert.AreEqual(42, loaded.Score);
        }

        [Test] public void EntityStore_Load_NonExistent_ReturnsNull()
        {
            var store = new EntityStore<TestEntity>(_repo, new TestSerializer(), "scores");
            Assert.IsNull(store.Load("nobody", "nothing"));
        }

        [Test] public void EntityStore_Delete_Works()
        {
            var store = new EntityStore<TestEntity>(_repo, new TestSerializer(), "scores");
            store.Save("alice", "run1", new TestEntity { Name = "A", Score = 1 }, "add");
            Assert.IsTrue(store.Exists("alice", "run1"));

            store.Delete("alice", "run1", "remove");
            Assert.IsFalse(store.Exists("alice", "run1"));
        }

        [Test] public void EntityStore_SharedSaveAndLoad()
        {
            var store = new EntityStore<TestEntity>(_repo, new TestSerializer(), "scores");
            var entity = new TestEntity { Name = "Shared", Score = 100 };

            store.SaveShared("leaderboard", entity, "publish");
            var loaded = store.LoadShared("leaderboard");
            Assert.AreEqual("Shared", loaded.Name);
            Assert.AreEqual(100, loaded.Score);
        }

        // ── GitMerge ────────────────────────────────────────────

        [Test] public void ThreeWayMerge_NoConflicts_BothApplied()
        {
            var baseF = new Dictionary<string, string> { { "a", "1" }, { "b", "2" } };
            var local = new Dictionary<string, string> { { "a", "10" }, { "b", "2" } };
            var incoming = new Dictionary<string, string> { { "a", "1" }, { "b", "20" } };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming);
            Assert.AreEqual("10", merged["a"]);
            Assert.AreEqual("20", merged["b"]);
        }

        [Test] public void ThreeWayMerge_Conflict_IncomingWins()
        {
            var baseF = new Dictionary<string, string> { { "x", "0" } };
            var local = new Dictionary<string, string> { { "x", "1" } };
            var incoming = new Dictionary<string, string> { { "x", "2" } };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming, GitMerge.Strategy.IncomingWins);
            Assert.AreEqual("2", merged["x"]);
        }

        [Test] public void ThreeWayMerge_Conflict_LocalWins()
        {
            var baseF = new Dictionary<string, string> { { "x", "0" } };
            var local = new Dictionary<string, string> { { "x", "1" } };
            var incoming = new Dictionary<string, string> { { "x", "2" } };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming, GitMerge.Strategy.LocalWins);
            Assert.AreEqual("1", merged["x"]);
        }

        [Test] public void ThreeWayMerge_Conflict_HigherWins()
        {
            var baseF = new Dictionary<string, string> { { "score", "0" } };
            var local = new Dictionary<string, string> { { "score", "50" } };
            var incoming = new Dictionary<string, string> { { "score", "75" } };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming, GitMerge.Strategy.HigherWins);
            Assert.AreEqual("75", merged["score"]);
        }

        [Test] public void ThreeWayMerge_Conflict_LowerWins()
        {
            var baseF = new Dictionary<string, string> { { "time", "999" } };
            var local = new Dictionary<string, string> { { "time", "42" } };
            var incoming = new Dictionary<string, string> { { "time", "55" } };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming, GitMerge.Strategy.LowerWins);
            Assert.AreEqual("42", merged["time"]);
        }

        [Test] public void ThreeWayMerge_PerFieldStrategy()
        {
            var baseF = new Dictionary<string, string> { { "score", "0" }, { "name", "old" } };
            var local = new Dictionary<string, string> { { "score", "50" }, { "name", "Alice" } };
            var incoming = new Dictionary<string, string> { { "score", "75" }, { "name", "Bob" } };

            var strategies = new Dictionary<string, GitMerge.Strategy>
            {
                { "score", GitMerge.Strategy.HigherWins },
                { "name", GitMerge.Strategy.LocalWins }
            };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming,
                GitMerge.Strategy.IncomingWins, strategies);

            Assert.AreEqual("75", merged["score"]);
            Assert.AreEqual("Alice", merged["name"]);
        }

        [Test] public void ThreeWayMerge_NewField_FromEitherSide()
        {
            var baseF = new Dictionary<string, string>();
            var local = new Dictionary<string, string> { { "a", "1" } };
            var incoming = new Dictionary<string, string> { { "b", "2" } };

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming);
            Assert.AreEqual("1", merged["a"]);
            Assert.AreEqual("2", merged["b"]);
        }

        [Test] public void ThreeWayMerge_LocalDelete_Respected()
        {
            var baseF = new Dictionary<string, string> { { "x", "1" } };
            var local = new Dictionary<string, string>(); // deleted
            var incoming = new Dictionary<string, string> { { "x", "1" } }; // unchanged

            var merged = GitMerge.ThreeWayMerge(baseF, local, incoming);
            Assert.IsFalse(merged.ContainsKey("x"));
        }

        [Test] public void TwoWayMerge_IncomingOverwritesLocal()
        {
            var local = new Dictionary<string, string> { { "a", "1" }, { "b", "2" } };
            var incoming = new Dictionary<string, string> { { "b", "20" }, { "c", "3" } };

            var merged = GitMerge.TwoWayMerge(local, incoming);
            Assert.AreEqual("1", merged["a"]);
            Assert.AreEqual("20", merged["b"]);
            Assert.AreEqual("3", merged["c"]);
        }

        // ── PlayerRegistry ──────────────────────────────────────

        const string SAMPLE_REGISTRY = @"{
  ""generated"": ""2026-03-14T04:00:00Z"",
  ""players"": {
    ""alice"": { ""repo"": ""alice/player-data"", ""joined"": ""2026-03-14"", ""programs"": [""autopilot"", ""scanner""], ""ships"": [""frigate""] },
    ""bob"":   { ""repo"": ""bob/player-data"",   ""joined"": ""2026-03-10"", ""programs"": [""nav-ai""] },
    ""charlie"": { ""repo"": ""charlie/player-data"", ""joined"": ""2026-03-01"", ""programs"": [], ""ships"": [] }
  }
}";

        [Test] public void PlayerRegistry_ParsesAllPlayers()
        {
            var registry = new PlayerRegistry();
            // Use reflection to call private ParseRegistry for unit test
            var method = typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(registry, new object[] { SAMPLE_REGISTRY });

            Assert.AreEqual(3, registry.PlayerCount);
        }

        [Test] public void PlayerRegistry_GetPlayer_ReturnsEntry()
        {
            var registry = new PlayerRegistry();
            typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(registry, new object[] { SAMPLE_REGISTRY });

            var alice = registry.GetPlayer("alice");
            Assert.IsNotNull(alice);
            Assert.AreEqual("alice/player-data", alice.repo);
            Assert.AreEqual("2026-03-14", alice.joined);
            Assert.AreEqual(2, alice.programs.Length);
            Assert.AreEqual("autopilot", alice.programs[0]);
            Assert.AreEqual("scanner", alice.programs[1]);
            Assert.AreEqual(1, alice.ships.Length);
            Assert.AreEqual("frigate", alice.ships[0]);
        }

        [Test] public void PlayerRegistry_GetPlayer_NotFound_ReturnsNull()
        {
            var registry = new PlayerRegistry();
            typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(registry, new object[] { SAMPLE_REGISTRY });

            Assert.IsNull(registry.GetPlayer("nobody"));
        }

        [Test] public void PlayerRegistry_Search_FindsByPrefix()
        {
            var registry = new PlayerRegistry();
            typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(registry, new object[] { SAMPLE_REGISTRY });

            var results = registry.Search("ali");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("alice", results[0].username);
        }

        [Test] public void PlayerRegistry_Search_CaseInsensitive()
        {
            var registry = new PlayerRegistry();
            typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(registry, new object[] { SAMPLE_REGISTRY });

            var results = registry.Search("BOB");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("bob", results[0].username);
        }

        [Test] public void PlayerRegistry_Search_EmptyQuery_ReturnsEmpty()
        {
            var registry = new PlayerRegistry();
            typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(registry, new object[] { SAMPLE_REGISTRY });

            Assert.AreEqual(0, registry.Search("").Count);
            Assert.AreEqual(0, registry.Search(null).Count);
        }

        [Test] public void PlayerRegistry_EmptyPrograms_ParsedAsEmptyArray()
        {
            var registry = new PlayerRegistry();
            typeof(PlayerRegistry).GetMethod("ParseRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(registry, new object[] { SAMPLE_REGISTRY });

            var charlie = registry.GetPlayer("charlie");
            Assert.IsNotNull(charlie);
            Assert.AreEqual(0, charlie.programs.Length);
            Assert.AreEqual(0, charlie.ships.Length);
        }
    }
}
