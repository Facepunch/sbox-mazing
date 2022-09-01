using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(0), ThreatValue(1, canBeOnlyEnemy: false)]
partial class EliteWanderer : Enemy
{
    public override float MoveSpeed => 81.5f;

    protected override int HoldType => 4;

    private float _invisTimer;
    private const float INVIS_DELAY = 0.2f;

    public override void Spawn()
    {
        base.Spawn();

        //SetAnimParameter("holdtype", 0);

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/hat/Bucket_Helmet/Models/bucket_helmet.clothing");
        AddClothingItem("models/citizen_clothes/vest/Chest_Armour/chest_armour.clothing");
        AddClothingItem("models/citizen_clothes/trousers/LegArmour/leg_armour.clothing");
        AddClothingItem("models/citizen_clothes/gloves/tactical_gloves/tactical_gloves.clothing");
        AddClothingItem("models/citizen_clothes/shoes/Boots/army_boots.clothing");
        Clothing.DressEntity(this);

        RenderColor = new Color(0.5f, 0f, 0f, 0.0f);

        Scale = 1.1f;
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
                //var opacity = Map(dist, 0f, 175f, 1.0f, 0.185f);
                var opacity = Map(dist, 0f, 180f, 1.0f, 0.15f);
                //RenderColor = new Color(0.5f, 0f, 0f, 0.0f);

                foreach (var child in Children.ToArray())
                {
                    if (child is ModelEntity e && e.Tags.Has("clothes"))
                    {
                        e.RenderColor = new Color(0.3f, 0.3f, 1f, opacity);
                    }
                }
            }

            _invisTimer = INVIS_DELAY;
        }
    }

    protected override void OnReachTarget()
    {
        TargetCell = GetRandomNeighborCell();
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
