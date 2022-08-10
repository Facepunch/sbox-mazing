using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn( FirstLevel = 2, SpawnPeriod = 4 )]
internal partial class Charger : Enemy
{
    public override float MoveSpeed => IsCharging ? 160f : 85f;

    public bool IsCharging { get; set; }

    private float _lookTimer;
    private const float LOOK_DELAY = 0.25f;

    public override void Spawn()
    {
        base.Spawn();

        new ModelEntity("models/citizen_clothes/hat/hat_securityhelmet.vmdl", this);
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        _lookTimer -= Time.Delta;
        if (_lookTimer <= 0f && !IsCharging)
        {
            var cell = this.GetCellIndex();
            var facingDir = this.GetFacingDirection();
            var wallCell = Game.CurrentMaze.RayCast(cell, facingDir);

            ScanForPlayer(cell, wallCell, facingDir);

            _lookTimer = LOOK_DELAY;
        }

        //if (IsCharging)
        //    DebugOverlay.Text("Charging", EyePosition, 0f, float.MaxValue);
    }

    private void ScanForPlayer(Mazing.GridCoord curr, Mazing.GridCoord end, Direction dir)
    {
        //DebugOverlay.Line(EyePosition, Game.CellCenterToPosition(end), 0.05f, false);

        while (curr != end)
        {
            curr += dir;

            if (Game.IsPlayerInCell(curr))
            {
                IsCharging = true;
                TargetCell = end;
                break;
            }
        }
    }

    protected override void OnReachTarget()
    {
        var cell = this.GetCellIndex();
        var facingDir = this.GetFacingDirection();

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
    }
}