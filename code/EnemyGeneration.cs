using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Enemies;
using Sandbox;

namespace Mazing;

public static partial class MazeGenerator
{
    private record struct EnemyType(
        TypeDescription Type,
        int FirstLevel,
        int Threat,
        int SpawnCount,
        bool CanBeOnlyEnemy,
        TypeDescription Replaces,
        int FullyReplaceLevel );

    [ConCmd.Client("mazing_spawntest")]
    public static void EnemyTypeTest()
    {
        var lastSpawnLevels = new Dictionary<TypeDescription, int>();

        for (var i = 0; i < 50; ++i)
        {
            var seed = Game.Random.Int(int.MaxValue - 1);
            var types = GetSpawningEnemyCounts(i, seed, lastSpawnLevels)
                .OrderByDescending(x => x.Count)
                .Select(x => $"{x.Type.Name} x{x.Count}")
                .ToArray();

            Log.Info($"Level {i + 1}: {string.Join(", ", types)}");
        }
    }

    public static (TypeDescription Type, int Count)[] GetSpawningEnemyCounts((TypeDescription Type, float Weight)[] types,
        int seed, int totalThreat)
    {
        var rand = new Random(seed);
        var spawned = new List<TypeDescription>();

        var usedTypes = types.Select(x =>
            {
                var info = GetEnemyTypeInfo(x.Type);

                return (info.Type, info.Threat, info.SpawnCount, x.Weight);
            })
            .ToArray();

        // Make sure at least one enemy of each chosen type spawns
        for (var i = 0; totalThreat > 0 && i < usedTypes.Length; ++i)
        {
            var type = usedTypes[i];

            if (type.Threat > totalThreat)
            {
                continue;
            }

            totalThreat -= type.Threat;

            for (var j = 0; j < type.SpawnCount; ++j)
            {
                spawned.Add(type.Type);
            }
        }

        // Spawn other enemies until the total threat is reached
        while (totalThreat > 0)
        {
            var next = usedTypes
                .Where(x => x.Threat <= totalThreat)
                .OrderBy(x => spawned.Count(y => y == x.Type) * x.Threat * x.SpawnCount / x.Weight)
                .FirstOrDefault();

            if (next.Type == null)
            {
                next = usedTypes.MinBy(x => x.Threat + Game.Random.Float() * 0.5f);
            }

            totalThreat -= next.Threat;

            for (var j = 0; j < next.SpawnCount; ++j)
            {
                spawned.Add(next.Type);
            }
        }

        return spawned
            .GroupBy(x => x)
            .Select(x => (Type: x.Key, Count: x.Count()))
            .ToArray();
    }

    private static EnemyType GetEnemyTypeInfo(TypeDescription x)
    {
        var threatAttrib = x.GetAttribute<ThreatValueAttribute>();
        var replacesAttrib = x.GetAttribute<ReplacesAttribute>();

        var firstLevel = x.GetAttribute<UnlockLevelAttribute>()?.Level ?? int.MaxValue;

        return new EnemyType(
            Type: x,
            FirstLevel: firstLevel,
            Threat: threatAttrib?.Value ?? 1,
            SpawnCount: threatAttrib?.SpawnCount ?? 1,
            CanBeOnlyEnemy: x.GetAttribute<CantBeOnlyEnemyAttribute>() == null,
            Replaces: replacesAttrib?.ReplacedType != null ? TypeLibrary.GetType(replacesAttrib.ReplacedType) : null,
            FullyReplaceLevel: firstLevel == int.MaxValue ? int.MaxValue : firstLevel + replacesAttrib?.LevelsUntilFullyReplaced ?? 0);
    }

    public static (TypeDescription Type, int Count)[] GetSpawningEnemyCounts( int levelIndex, int seed, Dictionary<TypeDescription, int> lastSpawnLevels )
    {
        var totalThreat = levelIndex == 0 ? 1 : levelIndex + 2;
        var levelNumber = levelIndex + 1;

        var unlocked = TypeLibrary.GetTypes<Enemy>()
            .Select(GetEnemyTypeInfo)
            .Where(x => x.FirstLevel <= levelNumber)
            .ToArray();

        var replacedBy = unlocked
            .Where(x => x.Replaces != null)
            .ToDictionary(x => x.Replaces, x => x);

        var justUnlocked = unlocked
            .Where(x => x.FirstLevel == levelNumber)
            .ToArray();

        var rand = new Random(seed);

        var alreadyUnlocked = unlocked
            .Where(x => x.FirstLevel < levelNumber && !replacedBy.ContainsKey(x.Type))
            .OrderByDescending(x => Math.Max(1f, lastSpawnLevels.TryGetValue(x.Type, out var lastLevel) ? levelIndex + 1f - lastLevel : 1f) * rand.NextSingle())
            .ToArray();

        // Make sure first enemy in list can be the only enemy in the level
        var canBeOnlyIndex = Array.FindIndex(alreadyUnlocked, x => x.CanBeOnlyEnemy);

        if (canBeOnlyIndex > 0)
        {
            (alreadyUnlocked[0], alreadyUnlocked[canBeOnlyIndex]) = (alreadyUnlocked[canBeOnlyIndex], alreadyUnlocked[0]);
        }

        // Choose which types of enemies will spawn:
        // * Any that have just unlocked are guaranteed to spawn
        // * Pick at least one other type too, if possible

        var canBeOnlyJustUnlocked = justUnlocked.Length > 1
            || justUnlocked.Length == 1 && justUnlocked[0].CanBeOnlyEnemy
            || alreadyUnlocked.Length == 0;

        var totalCount = canBeOnlyJustUnlocked
            ? rand.Next(justUnlocked.Length, Math.Max(justUnlocked.Length, 3) + 1)
            : rand.Next(justUnlocked.Length + 1, Math.Max(justUnlocked.Length + 1, 3) + 1);

        if (justUnlocked.Length + alreadyUnlocked.Length > 1 && rand.NextSingle() < 0.8f)
        {
            totalCount = Math.Max(2, totalCount);
        }

        var usedTypes = justUnlocked
            .Concat(alreadyUnlocked.Take(totalCount - justUnlocked.Length))
            .Select(x => (x.Type, x.Threat, x.SpawnCount, x.Replaces,
                Weight: x.Replaces == null || x.FullyReplaceLevel <= levelNumber ? 1f
                : (levelNumber - x.FirstLevel + 1f) / (x.FullyReplaceLevel - x.FirstLevel + 1)))
            .ToList();

        for (var i = usedTypes.Count - 1; i >= 0; --i)
        {
            var replacer = usedTypes[i];
            if (replacer.Replaces == null || replacer.Weight >= 1f) continue;

            var replaced = unlocked.FirstOrDefault(x => x.Type == replacer.Replaces);
            if (replaced.Type == null) continue;

            usedTypes.Add((replaced.Type, replaced.Threat, replaced.SpawnCount, null, Weight: 1f - replacer.Weight));
        }

        var types = GetSpawningEnemyCounts(
            usedTypes.Select(x => (x.Type, x.Weight)).ToArray(),
            rand.Next(), totalThreat);

        foreach (var type in types)
        {
            lastSpawnLevels[type.Type] = levelIndex;
        }

        return types;
    }
}