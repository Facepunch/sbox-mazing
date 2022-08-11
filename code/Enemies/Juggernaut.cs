using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

partial class Juggernaut : Enemy
{
    public override float MoveSpeed => 81f;

    public override void Spawn()
    {
        base.Spawn();

        //new ModelEntity("models/citizen_clothes/hair/hair_shortscruffy/Models/hair_shortscruffy_grey.vmdl", this);
        Scale = 1.5f;
        RenderColor = new Color(0f, 0.2f, 0.6f);
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        DebugOverlay.Text((TargetCell - this.GetCellIndex()).Distance.ToString(), EyePosition, 0f, float.MaxValue);
    }

    protected override void OnReachTarget()
    {
        var player = Game.GetClosestPlayer(Position);

        if (player == null)
        {
            TargetCell = GetRandomNeighborCell();
        }
        else
        {
            TargetCell = GetNextInPathTo(player.GetCellIndex());

            if((TargetCell - this.GetCellIndex()).Distance == 0)
            {
                Game.DestroyWall(this.GetCellIndex(), MazeData.GetDirection(player.Position - Position));
            }

            //if (TargetCell - this.GetCellIndex().Distance)
        }

        //TargetCell = GetRandomNeighborCell();

        //Game.DestroyWall(this.GetCellIndex(), this.GetFacingDirection());
    }
}