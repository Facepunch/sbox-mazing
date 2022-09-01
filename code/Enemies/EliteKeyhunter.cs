using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Items;
using Mazing.Player;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(40), ThreatValue(6)]
partial class EliteKeyhunter : Enemy
{
    public override string NounPhrase => "an Elite Keyhunter";

    private const float HuntStartDelay = 0.2f;

    public override float MoveSpeed => (IsHuntingPlayer || Game.Key == null) && _huntStartTime > HuntStartDelay ? 99f : 70f;

    protected override int HoldType => (IsHuntingPlayer || Game.Key == null) ? 4 : 0;

    public bool IsHuntingPlayer { get; private set; }
    private Mazing.Player.MazingPlayer _huntedPlayer;

    public override Vector3 LookPos 
    {
        get
        {
            var player = All.OfType<MazingPlayer>().FirstOrDefault( x => x.HeldEntity != null );
            return player != null ? player.EyePosition.WithZ(player.EyePosition.z * 0.25f) : base.LookPos;
        }
    }

    private Color _colorNormal = new Color(0.3f, 0f, 0.1f);
    private Color _colorHunting = new Color(1f, 0.25f, 0.66f);

    private bool _wasHuntingKey;
    private TimeSince _huntStartTime;

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin04.clothing");
        AddClothingItem("models/citizen_clothes/hair/hair_bun/hair_bun.clothing");
        AddClothingItem("models/citizen_clothes/trousers/CardboardTrousers/cardboard_trousers.clothing");
        AddClothingItem("models/citizen_clothes/vest/Cardboard_Chest/cardboard_chest.clothing");
        Clothing.DressEntity(this);

        foreach (var child in Children.ToArray())
        {
            if (child is ModelEntity e && e.Tags.Has("clothes"))
            {
                e.RenderColor = new Color(1f, 0f, 0.1f, 1f);
            }
        }

        Scale = 1.225f;
    }

    protected override void OnReachTarget()
    {
        if ( IsHuntingPlayer && _huntedPlayer != null && _huntStartTime > HuntStartDelay )
        {
            TargetCell = GetNextInPathTo(_huntedPlayer.Position );
        }
        else
        {
            TargetCell = GetRandomNeighborCell();
        }
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        if (Game.Key?.Parent != null && Game.Key.Parent is MazingPlayer)
        {
            if(!IsHuntingPlayer || _huntedPlayer != Game.Key.Parent)
            {
                IsHuntingPlayer = true;
                _huntedPlayer = (MazingPlayer)Game.Key.Parent;

                Sound.FromEntity("keyhunter.alert", this);
                _huntStartTime = 0f;
                OnReachTarget();
            }
        }

        if(IsHuntingPlayer && (_huntedPlayer == null || _huntedPlayer.HasExited || !_huntedPlayer.IsAlive))
        {
            IsHuntingPlayer = false;
            _huntedPlayer = null;
        }

        RenderColor = Color.Lerp( _colorNormal, _colorHunting, IsHuntingPlayer ? _huntStartTime / HuntStartDelay : 0f );

        //if (IsHuntingPlayer && _huntedPlayer != null)
        //    DebugOverlay.Line(EyePosition, _huntedPlayer.EyePosition, 0f, false);
    }

    public bool IsHuntingKey()
    {
        return Game.Key == null || Game.Key.IsHeld;
    }
}