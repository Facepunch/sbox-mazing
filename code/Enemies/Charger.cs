using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(3), ThreatValue(3)]
internal partial class Charger : Enemy
{
    public override float MoveSpeed => IsCharging ? 163f : 85f;

    private bool _isCharging;

    protected override int HoldType => _isCharging ? 4 : 0;

    public bool IsCharging
    {
        get => _isCharging;
        set
        {
            _isCharging = value;
            RenderColor = IsCharging ? new Color( 1f, 0f, 0f, 1f ) : Color.White;
        }
    }

    private float _lookTimer;
    private const float LOOK_DELAY = 0.25f;

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin04.clothing");
        AddClothingItem("models/citizen_clothes/hat/hardhat.yellow.clothing");
        AddClothingItem("models/citizen_clothes/shoes/SmartShoes/smartshoes.clothing");
        Clothing.DressEntity(this);

        Scale = 1.1f;
    }

    protected override void OnLevelChange()
    {
        base.OnLevelChange();

        IsCharging = false;
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        if ( !IsAwake )
        {
            return;
        }

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
                if ( !IsCharging )
                {
                    Sound.FromEntity( "charger.alert", this );
                }

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

            Sound.FromEntity( "charger.hitwall", this );

            // TODO: look left and right for a player
        }
    }
}