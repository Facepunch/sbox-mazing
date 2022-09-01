using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(2), ThreatValue(2)]
partial class Seeker : Enemy
{
    public override string NounPhrase => "a Seeker";

    public override float MoveSpeed => 81f;

    //protected override int HoldType => 4;
    public override Vector3 LookPos
    {
        get
        {
            var player = Game.GetClosestPlayer(Position);

            return player != null ? player.EyePosition.WithZ(player.EyePosition.z * 0.5f) : base.LookPos;
        }
    }

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin05.clothing");
        //AddClothingItem("models/citizen_clothes/hair/hair_balding/hair_baldinggrey.clothing");
        Clothing.DressEntity(this);

        Scale = 0.8f;
    }

    protected override void OnReachTarget()
    {
        var player = Game.GetClosestPlayer( Position );

        if ( player == null )
        {
            TargetCell = GetRandomNeighborCell();
        }
        else
        {
            TargetCell = GetNextInPathTo( player.Position );
        }
    }
}