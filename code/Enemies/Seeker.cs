using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 2, SpawnPeriod = 4)]
partial class Seeker : Enemy
{
    public override float MoveSpeed => 100f;

    public override void Spawn()
    {
        base.Spawn();

        new ModelEntity( "models/citizen_clothes/hat/hat_beret.black.vmdl", this );
    }

    protected override void OnReachTarget()
    {
        var player = Entity.All.OfType<MazingPlayer>()
            .Where( x => x.IsAliveInMaze )
            .MinBy( x => (x.Position - Position).LengthSquared );

        if ( player == null )
        {
            TargetCell = GetRandomNeighborCell();
        }
        else
        {
            TargetCell = GetNextInPathTo( player.GetCellIndex() );
        }
    }
}