using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(9), ThreatValue(4)]
partial class Keyhunter : Enemy
{
    public override float MoveSpeed => IsHuntingKey() ? 99f : 60f;

    protected override int HoldType => IsHuntingKey() ? 4 : 0;

    public override Vector3 LookPos => GetLookPos();

    private Color _colorNormal = new Color(0.66f, 0.66f, 0.3f);
    private Color _colorHunting = new Color(1f, 1f, 0f);

    private bool _wasHuntingKey;

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
        if ( Game.Key?.IsHeld ?? false )
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
        }

        _wasHuntingKey = huntingKey;

        RenderColor = huntingKey ? _colorHunting : _colorNormal;
    }

    public bool IsHuntingKey()
    {
        var key = Entity.All.OfType<Key>().FirstOrDefault();
        return (key == null || key.IsHeld);
    }

    private Vector3 GetLookPos()
    {
        var player = Entity.All.OfType<MazingPlayer>().Where(x => x.HeldItem != null).FirstOrDefault();

        if (player != null)
        {
            return player.EyePosition.WithZ(player.EyePosition.z * 0.25f);
        }
        else
        {
            return EyePosition + EyeRotation.Forward * 200;
        }
    }
}