using Sandbox;
using System.Linq;
using Mazing.Player;

namespace Mazing.Enemies;

[UnlockLevel(25), ThreatValue(5), Replaces(typeof(Charger), 15)]
internal partial class EliteCharger : Enemy
{
    public override string NounPhrase => "an Elite Charger";

    public override float MoveSpeed => IsCharging || IsHunting ? 163f : 85f;

    private bool _isHunting;
    private Mazing.Player.MazingPlayer _huntedPlayer;
    private GridCoord _lastKnownPlayerCell;

    private bool _isCharging;

    protected override int HoldType => _isCharging || _isHunting ? 4 : 0;

    public bool IsHunting
    {
        get => _isHunting;
        set
        {
            _isHunting = value;
            RenderColor = IsHunting ? new Color(1f, 0f, 1f, 1f) : (IsCharging ? new Color( 1f, 0f, 0.4f, 1f ) : new Color(1f, 0.75f, 1f, 1f));
        }
    }

    public bool IsCharging
    {
        get => _isCharging;
        set
        {
            _isCharging = value;
            RenderColor = IsHunting ? new Color(1f, 0f, 1f, 1f) : (IsCharging ? new Color(1f, 0f, 0.4f, 1f) : new Color(1f, 0.75f, 1f, 1f));
        }
    }

    private float _lookTimer;
    private const float LOOK_DELAY = 0.25f;

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin04.clothing");
        AddClothingItem("models/citizen_clothes/hat/Hard_Hat/hard_hat.clothing");
        AddClothingItem("models/citizen_clothes/gloves/leather_gloves/leather_gloves.clothing");
        AddClothingItem("models/citizen_clothes/shoes/Boots/army_boots.clothing");
        Clothing.DressEntity(this);

        foreach (var child in Children.ToArray())
        {
            if (child is ModelEntity e && e.Tags.Has("clothes"))
            {
                e.RenderColor = new Color(0f, 0f, 0f, 1f);
            }
        }

        Scale = 1.2f;
    }

    protected override void OnLevelChange()
    {
        base.OnLevelChange();

        IsCharging = false;
        IsHunting = false;
        _huntedPlayer = null;
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        //DebugOverlay.Text("Charging: " + IsCharging.ToString() + "\nHunting: " + IsHunting.ToString() + "\n_huntedPlayerVaulted: " + _huntedPlayerVaulted.ToString(), EyePosition, 0f, float.MaxValue);
        //if(_huntedPlayerVaulted)
        //    DebugOverlay.Line(EyePosition, Game.CellCenterToPosition(_vaultCell), 0f, false);

        if ( !IsAwake )
        {
            return;
        }

        _lookTimer -= Time.Delta;
        if (_lookTimer <= 0f && !IsCharging && !IsHunting)
        {
            var cell = this.GetCellIndex();
            var facingDir = this.GetFacingDirection();
            var wallCell = Game.CurrentMaze.RayCast(cell, facingDir);

            ScanForPlayer(cell, wallCell, facingDir);

            _lookTimer = LOOK_DELAY;
        }

        if (!IsHunting || _huntedPlayer == null) return;

        if (!_huntedPlayer.IsValid || _huntedPlayer.IsVaulting || !_huntedPlayer.IsAliveInMaze)
        {
            _huntedPlayer = null;
            return;
        }
        
        _lastKnownPlayerCell = _huntedPlayer.GetCellIndex();
    }

    protected void ScanForPlayer(Mazing.GridCoord curr, Mazing.GridCoord end, Direction dir)
    {
        while (curr != end)
        {
            curr += dir;

            if (!Game.IsPlayerInCell(curr)) continue;

            var player = Game.GetPlayerInCell(curr);
            if (player == null || player.IsVaulting) continue;

            if ( !IsCharging && !IsHunting )
            {
                Sound.FromEntity( "charger.alert", this );
            }

            _huntedPlayer = player;
            _lastKnownPlayerCell = player.GetCellIndex();

            IsHunting = true;
            TargetCell = GetNextInPathTo(_huntedPlayer.Position);

            IsCharging = false;

            break;
        }
    }

    protected override void OnReachTarget()
    {
        var cell = this.GetCellIndex();
        var facingDir = this.GetFacingDirection();

        if (IsHunting)
        {
            if (_huntedPlayer != null)
            {
                TargetCell = GetNextInPathTo(_huntedPlayer.Position);
            }
            else if (cell == _lastKnownPlayerCell)
            {
                IsHunting = false;
                _huntedPlayer = null;

                IsCharging = true;

                var wallCell = Game.CurrentMaze.RayCast(cell, facingDir);
                TargetCell = wallCell;
            }
            else
            {
                TargetCell = GetNextInPathTo(_lastKnownPlayerCell);
            }
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