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
    public override float MoveSpeed => 100f;

    protected override int HoldType => 0;

    public override void Spawn()
    {
        base.Spawn();

        SetAnimParameter("holdtype", 0);

        new ModelEntity( "models/citizen_clothes/hat/hat_hardhat.vmdl", this );
    }

    protected override void OnReachTarget()
    {
        TargetCell = GetRandomNeighborCell();
    }
}
