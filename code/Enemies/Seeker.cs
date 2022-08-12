using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(0)]
partial class Seeker : Enemy
{
    public override float MoveSpeed => 84f;

    //protected override int HoldType => 4;
    public override Vector3 LookPos => GetLookPos();

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin05.clothing");
        //AddClothingItem("models/citizen_clothes/vest/Chest_Armour/chest_armour.clothing");
        //AddClothingItem("models/citizen_clothes/trousers/LegArmour/leg_armour.clothing");
        //AddClothingItem("models/citizen_clothes/gloves/tactical_gloves/tactical_gloves.clothing");
        //AddClothingItem("models/citizen_clothes/shoes/Boots/army_boots.clothing");
        Clothing.DressEntity(this);

        SetAnimParameter("b_vr", true);
        SetAnimParameter("aim_body_weight", 0.75f);
        SetAnimParameter("left_hand_ik.position", new Vector3(56f, 14f, 64f));
        SetAnimParameter("right_hand_ik.position", new Vector3(6f, -114f, 164f));

        //SetAnimParameter("holdtype", 4);

        //SetModel("models/citizen_mannequin/mannequin.vmdl");
        //new ModelEntity( "models/citizen_clothes/hat/hat_beret.black.vmdl", this );
        //new ModelEntity("models/citizen_clothes/gloves/tactical_gloves/Models/tactical_gloves.vmdl", this);
        //new ModelEntity("models/citizen_clothes/glasses/Stylish_Glasses/Models/stylish_glasses_black.vmdl", this);
        //RenderColor = Color.Green;
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

    private Vector3 GetLookPos()
    {
        var player = Game.GetClosestPlayer(Position);

        if (player != null)
        {
            return player.EyePosition.WithZ(player.EyePosition.z * 0.5f);
        }
        else
        {
            return EyePosition + EyeRotation.Forward * 200;
        }
    }
}