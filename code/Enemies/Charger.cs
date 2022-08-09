using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn( FirstLevel = 0 )]
internal partial class Charger : Enemy
{
    public override float MoveSpeed => IsCharging ? 160f : 90f;

    public bool IsCharging { get; set; }

    public override void Spawn()
    {
        base.Spawn();

        new ModelEntity("models/citizen_clothes/hat/hat_securityhelmet.vmdl", this);
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        // TODO: check more frequently
    }

    protected override void OnReachTarget()
    {
        var cell = this.GetCellIndex();
        var facingDir = this.GetFacingDirection();

        if ( !IsCharging )
        {
            var wallCoord = Game.CurrentMaze.RayCast(cell, facingDir);

            while ( cell != wallCoord )
            {
                cell += facingDir;

                if ( Game.IsPlayerInCell( cell ) )
                {
                    IsCharging = true;
                    break;
                }
            }
        }

        if ( !IsCharging )
        {
            TargetCell = GetRandomNeighborCell();
        }
        else if ( Game.CurrentMaze.GetWall( cell, facingDir ) )
        {
            IsCharging = false;
            TargetCell = GetRandomNeighborCell();

            // TODO: look left and right for a player
        }
        else
        {
            TargetCell = cell + facingDir;
        }
    }
}