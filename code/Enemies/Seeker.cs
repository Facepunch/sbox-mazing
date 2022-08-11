using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 1, SpawnPeriod = 4)]
partial class Seeker : Enemy
{
    public override float MoveSpeed => 85f;

    public override void Spawn()
    {
        base.Spawn();

        //SetModel("models/citizen_mannequin/mannequin.vmdl");
        //new ModelEntity( "models/citizen_clothes/hat/hat_beret.black.vmdl", this );
        new ModelEntity("models/citizen_clothes/gloves/tactical_gloves/Models/tactical_gloves.vmdl", this);
        new ModelEntity("models/citizen_clothes/glasses/Stylish_Glasses/Models/stylish_glasses_black.vmdl", this);
        RenderColor = Color.Green;
    }

    protected override void OnReachTarget()
    {
        var player = Entity.All.OfType<MazingPlayer>()
            .Where( x => x.IsAliveInMaze )
            .MinBy( x => (x.Position - Position).LengthSquared );

        if ( player == null )
        {
            TargetCell = GetRandomNeighborCell();
        }
        else
        {
            TargetCell = GetNextInPathTo( player.GetCellIndex() );
        }
    }
}