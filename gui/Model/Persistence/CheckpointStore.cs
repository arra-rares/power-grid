using System.IO;
using System.Text;
using System.Text.Json;
using Serilog;

namespace gui.Model.Persistence
{
    public sealed class SavedCheckpoint
    {
        public required GameSnapshot Snapshot { get; init; }
        public required string Path { get; init; }
        public int Generation => Snapshot.Generation;

        public string Display =>
            $"Round {Snapshot.RoundNumber} · {Snapshot.Phase} · {Snapshot.SavedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    }

    public sealed class CheckpointStore
    {
        public const int SchemaVersion = 1;
        public const int MaxKept = 3;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string _directory;

        public CheckpointStore(string? directory = null)
        {
            _directory = directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerGrid",
                "Saves");
        }

        public IReadOnlyList<SavedCheckpoint> LoadValid(Func<int, bool> cardExists)
        {
            return ReadValid(cardExists).Take(MaxKept).ToList();
        }

        public void Commit(GameSnapshot snapshot)
        {
            Directory.CreateDirectory(_directory);
            snapshot.SchemaVersion = SchemaVersion;
            snapshot.Generation = NextGeneration();
            snapshot.SavedAt = DateTimeOffset.UtcNow;

            if (!snapshot.TryValidate(_ => true, out var error))
                throw new InvalidOperationException($"Refusing to write an invalid checkpoint: {error}");

            var destination = Path.Combine(_directory, $"checkpoint-{snapshot.Generation}.json");
            if (File.Exists(destination))
                throw new IOException($"Checkpoint file already exists: {destination}");

            var temp = Path.Combine(_directory, "checkpoint-writing.tmp");
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, JsonOptions));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            File.Move(temp, destination);

            if (!TryRead(destination, _ => true, out var written, out error) || written!.Generation != snapshot.Generation)
                throw new IOException($"Checkpoint {destination} failed read-back: {error}");

            Log.Information("Checkpoint generation {Generation} saved for round {Round} {Phase}",
                snapshot.Generation, snapshot.RoundNumber, snapshot.Phase);

            foreach (var extra in ReadValid(_ => true).Skip(MaxKept))
            {
                try
                {
                    File.Delete(extra.Path);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not prune checkpoint {Path}", extra.Path);
                }
            }
        }

        private List<SavedCheckpoint> ReadValid(Func<int, bool> cardExists)
        {
            if (!Directory.Exists(_directory))
                return [];

            var parsed = new List<SavedCheckpoint>();
            foreach (var path in Directory.GetFiles(_directory, "checkpoint-*.json"))
            {
                if (!TryRead(path, cardExists, out var snapshot, out var error))
                {
                    Log.Warning("Ignoring checkpoint {Path}: {Error}", path, error);
                    continue;
                }
                parsed.Add(new SavedCheckpoint { Snapshot = snapshot!, Path = path });
            }

            return parsed.OrderByDescending(item => item.Generation).ToList();
        }

        private int NextGeneration()
        {
            if (!Directory.Exists(_directory))
                return 1;

            var max = 0;
            foreach (var path in Directory.GetFiles(_directory, "checkpoint-*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                const string prefix = "checkpoint-";
                if (name.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(name[prefix.Length..], out var generation))
                {
                    max = Math.Max(max, generation);
                }
            }
            return max + 1;
        }

        public void RetireAll()
        {
            if (!Directory.Exists(_directory))
                return;
            foreach (var path in Directory.GetFiles(_directory, "checkpoint-*.json"))
                RetireFile(path);
        }

        public void RetireNewerThan(int generation)
        {
            if (!Directory.Exists(_directory))
                return;

            var newer = new List<(int Generation, string FilePath)>();
            foreach (var path in Directory.GetFiles(_directory, "checkpoint-*.json"))
            {
                if (TryRead(path, _ => true, out var snapshot, out _))
                    newer.Add((snapshot!.Generation, path));
            }

            foreach (var item in newer.Where(item => item.Generation > generation).OrderByDescending(item => item.Generation))
                RetireFile(item.FilePath);
        }

        private void RetireFile(string path)
        {
            var retired = Path.Combine(_directory, "retired");
            Directory.CreateDirectory(retired);
            var destination = Path.Combine(retired, Path.GetFileName(path));
            if (File.Exists(destination))
                destination = Path.Combine(retired, $"{Guid.NewGuid():N}-{Path.GetFileName(path)}");
            File.Move(path, destination);
            Log.Information("Retired checkpoint {Path}", path);
        }

        private static bool TryRead(string path, Func<int, bool> cardExists, out GameSnapshot? snapshot, out string error)
        {
            snapshot = null;
            error = "";
            try
            {
                var json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<GameSnapshot>(json, JsonOptions);
                if (parsed == null)
                {
                    error = "Empty checkpoint.";
                    return false;
                }
                if (!parsed.TryValidate(cardExists, out error))
                    return false;
                snapshot = parsed;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
