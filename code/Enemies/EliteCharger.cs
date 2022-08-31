using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(0), ThreatValue(1)]
internal partial class EliteCharger : Enemy
{
    public override float MoveSpeed => IsCharging || IsHunting ? 163f : 85f;

    private bool _isHunting;
    private Mazing.Player.MazingPlayer _huntedPlayer;
    private bool _wasHuntedPlayerVaulting;

    private bool _isCharging;

    protected override int HoldType => _isCharging || _isHunting ? 4 : 0;

    public bool IsHunting
    {
        get => _isHunting;
        set
        {
            _isHunting = value;
            RenderColor = IsCharging || IsHunting ? new Color( 1f, 0f, 1f, 1f ) : new Color(0.8f, 0.5f, 1f, 1f);
        }
    }

    public bool IsCharging
    {
        get => _isCharging;
        set
        {
            _isCharging = value;
            RenderColor = IsCharging || IsHunting ? new Color(1f, 0f, 1f, 1f) : new Color(0.8f, 0.5f, 1f, 1f);
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
        AddClothingItem("models/citizen_clothes/gloves/leather_gloves/leather_gloves.clothing");
        Clothing.DressEntity(this);

        Scale = 1.2f;
    }

    protected override void OnLevelChange()
    {
        base.OnLevelChange();

        IsCharging = false;
        IsHunting = false;
        _huntedPlayer = null;
        _wasHuntedPlayerVaulting = false;
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

        if(IsHunting && _huntedPlayer != null)
        {
            //DebugOverlay.Line(EyePosition, _huntedPlayer.EyePosition, 0.05f, false);

            if(!_wasHuntedPlayerVaulting && _huntedPlayer.IsVaulting)
            {
                IsHunting = false;
                _huntedPlayer = null;

                IsCharging = true;

                var cell = this.GetCellIndex();
                var facingDir = this.GetFacingDirection();
                var wallCell = Game.CurrentMaze.RayCast(cell, facingDir);
                TargetCell = wallCell;
            }

            if (_wasHuntedPlayerVaulting && !_huntedPlayer.IsVaulting)
                _wasHuntedPlayerVaulting = false;
        }
    }

    protected void ScanForPlayer(Mazing.GridCoord curr, Mazing.GridCoord end, Direction dir)
    {
        while (curr != end)
        {
            curr += dir;

            if (Game.IsPlayerInCell(curr))
            {
                if ( !IsCharging )
                {
                    Sound.FromEntity( "charger.alert", this );
                }

                _huntedPlayer = Game.GetPlayerInCell(curr);
                if (_huntedPlayer != null)
                {
                    IsHunting = true;
                    TargetCell = GetNextInPathTo(_huntedPlayer.Position);
                    _wasHuntedPlayerVaulting = _huntedPlayer.IsVaulting;
                }

                IsCharging = false;

                break;
            }
        }
    }

    protected override void OnReachTarget()
    {
        var cell = this.GetCellIndex();
        var facingDir = this.GetFacingDirection();

        if(IsHunting && _huntedPlayer != null)
        {
            TargetCell = GetNextInPathTo(_huntedPlayer.Position);
        }
        else if ( !IsCharging )
        {
            TargetCell = GetRandomNeighborCell();
        }
        else if ( IsCharging && Game.CurrentMaze.GetWall( cell, facingDir ) )
        {
            IsCharging = false;
            TargetCell = GetRandomNeighborCell();

            Sound.FromEntity( "charger.hitwall", this );
        }
    }
}