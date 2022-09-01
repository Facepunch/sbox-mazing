using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(1), ThreatValue(1), CantBeOnlyEnemy]
partial class Wanderer : Enemy
{
    public override string NounPhrase => "a Wanderer";
    public override float MoveSpeed => 82f;

    protected override int HoldType => 4;

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
    }

    protected override void OnReachTarget()
    {
        TargetCell = GetRandomNeighborCell();
    }
}
