using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Items;
using Mazing.Player;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(8), ThreatValue(4)]
partial class Keyhunter : Enemy
{
    public override string NounPhrase => "a Keyhunter";

    private const float HuntStartDelay = 0.5f;

    public override float MoveSpeed => IsHuntingKey() && _huntStartTime > HuntStartDelay ? 99f : 60f;

    protected override int HoldType => IsHuntingKey() ? 4 : 0;

    public override Vector3 LookPos 
    {
        get
        {
            var player = All.OfType<MazingPlayer>().FirstOrDefault( x => x.HeldEntity != null );
            return player != null ? player.EyePosition.WithZ(player.EyePosition.z * 0.25f) : base.LookPos;
        }
    }

    private Color _colorNormal = new Color(0.3f, 0.3f, 0.1f);
    private Color _colorHunting = new Color(1f, 1f, 0f);

    private bool _wasHuntingKey;
    private TimeSince _huntStartTime;

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin04.clothing");
        AddClothingItem("models/citizen_clothes/hair/hair_fade/hair_fade.clothing");
        Clothing.DressEntity(this);

        Scale = 1.225f;
    }

    protected override void OnReachTarget()
    {
        if ( (Game.Key?.IsHeld ?? false) && _huntStartTime > HuntStartDelay )
        {
            TargetCell = GetNextInPathTo( Game.Key.Position );
        }
        else
        {
            TargetCell = GetRandomNeighborCell();
        }
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        var huntingKey = IsHuntingKey();

        if ( huntingKey && !_wasHuntingKey )
        {
            Sound.FromEntity( "keyhunter.alert", this );
            _huntStartTime = 0f;

            OnReachTarget();
        }

        _wasHuntingKey = huntingKey;

        RenderColor = Color.Lerp( _colorNormal, _colorHunting, huntingKey ? _huntStartTime / HuntStartDelay : 0f );
    }

    public bool IsHuntingKey()
    {
        return Game.Key == null || Game.Key.IsHeld;
    }
}