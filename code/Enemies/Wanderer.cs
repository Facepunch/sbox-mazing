﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 0, LastLevel = 0)]
[EnemySpawn(FirstLevel = 1, SpawnPeriod = 4)]
partial class Wanderer : Enemy
{
    public override float MoveSpeed => 100f;

    public override void Spawn()
    {
        base.Spawn();

        new ModelEntity( "models/citizen_clothes/hat/hat_hardhat.vmdl", this );
    }

    protected override void OnReachTarget()
    {
        TargetCell = GetRandomNeighborCell();
    }
}
