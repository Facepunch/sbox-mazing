using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Items;

namespace Mazing;

public static partial class MazeGenerator
{
    public static (TreasureKind Kind, int Count)[] GetSpawningTreasureCounts(int totalValue, int seed)
    {
        var rand = new Random(seed);
        var possibleKinds = new List<TreasureKind>(Enum.GetValues<TreasureKind>());
        var spawned = new List<TreasureKind>();

        while (totalValue > 0 && possibleKinds.Count > 0)
        {
            var chosenKind = possibleKinds[rand.Next(0, possibleKinds.Count)];
            var value = Treasure.GetValue(chosenKind);

            if (value > totalValue)
            {
                possibleKinds.Remove(chosenKind);
                continue;
            }

            totalValue -= value;
            spawned.Add(chosenKind);
        }

        return spawned
            .GroupBy(x => x)
            .Select(x => (x.Key, x.Count()))
            .ToArray();
    }
}
