using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(0)]
partial class Wanderer : Enemy
{
    public override float MoveSpeed => 88f;

    protected override int HoldType => 0;

    public override void Spawn()
    {
        base.Spawn();

        SetAnimParameter("holdtype", 0);

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/hat/Bucket_Helmet/Models/bucket_helmet.clothing");
        AddClothingItem("models/citizen_clothes/vest/Chest_Armour/chest_armour.clothing");
        AddClothingItem("models/citizen_clothes/trousers/LegArmour/leg_armour.clothing");
        AddClothingItem("models/citizen_clothes/gloves/tactical_gloves/tactical_gloves.clothing");
        AddClothingItem("models/citizen_clothes/shoes/Boots/army_boots.clothing");
        Clothing.DressEntity(this);
    }

    protected override void OnReachTarget()
    {
        TargetCell = GetRandomNeighborCell();
    }
}
