using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 2, SpawnPeriod = 2)]
partial class Seeker : Enemy
{
    public override float MoveSpeed => 100f;

    public override void Spawn()
    {
        base.Spawn();

        new ModelEntity("models/citizen_clothes/hat/hat_beret.black.vmdl", this);

        SetBodyGroup(1, 0);
    }

    protected override void OnReachTarget()
    {
        var cell = CurrentCellIndex;

        var direction = MazeData.Directions.Where(x => CanWalkInDirection(x.Direction))
            .OrderBy(x => Rand.Float() - GetSinceLastVisited(cell.Row + x.DeltaRow, cell.Col + x.DeltaCol))
            .FirstOrDefault();

        TargetCell = (TargetCell.Row + direction.DeltaRow, TargetCell.Col + direction.DeltaCol);
    }
}