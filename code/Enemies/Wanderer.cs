using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

partial class Wanderer : Enemy
{
    public override float MoveSpeed => 100f;

    private TimeSince[,] _cellVisitTimes;

    protected override void OnRespawn()
    {
        _cellVisitTimes = new TimeSince[Game.CurrentMaze.Rows, Game.CurrentMaze.Cols];
    }

    protected override void OnReachTarget()
    {
        var cell = CurrentCellIndex;

        _cellVisitTimes[cell.Row, cell.Col] = 0f;

        var direction = MazeData.Directions.Where(x => CanWalkInDirection(x.Direction))
            .OrderBy(x => Rand.Float() - _cellVisitTimes[cell.Row + x.DeltaRow, cell.Col + x.DeltaCol])
            .FirstOrDefault();

        TargetCell = (TargetCell.Row + direction.DeltaRow, TargetCell.Col + direction.DeltaCol);
    }
}
