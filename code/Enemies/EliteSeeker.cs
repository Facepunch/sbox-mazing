using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(0), ThreatValue(1)]
partial class EliteSeeker : Enemy
{
    public override float MoveSpeed => 80.4f;

    private float _invisTimer;
    private const float INVIS_DELAY = 0.2f;

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
        AddClothingItem("models/citizen_clothes/necklace/necklace/necklace.clothing");
        Clothing.DressEntity(this);

        Scale = 0.65f;
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        _invisTimer -= Time.Delta;
        if (_invisTimer <= 0f)
        {
            var player = Game.GetClosestPlayer(Position);
            if (player != null)
            {
                var dist = (player.Position.WithZ(0) - Position.WithZ(0)).Length;
                var opacity = Map(dist, 40f, 175f, 1.0f, 0.0f);
                RenderColor = new Color(0.5f, 0f, 0f, opacity);
            }

            _invisTimer = INVIS_DELAY;
        }
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

    private float Map(float value, float inputMin, float inputMax, float outputMin, float outputMax, bool clamp = true)
    {
        if (inputMin.Equals(inputMax) || outputMin.Equals(outputMax))
            return outputMin;

        if (clamp)
        {
            if (inputMax > inputMin)
            {
                if (value < inputMin) value = inputMin;
                else if (value > inputMax) value = inputMax;
            }
            else if (inputMax < inputMin)
            {
                if (value > inputMin) value = inputMin;
                else if (value < inputMax) value = inputMax;
            }
        }

        var ratio = (value - inputMin) / (inputMax - inputMin);
        var outVal = outputMin + ratio * (outputMax - outputMin);

        return outVal;
    }
}