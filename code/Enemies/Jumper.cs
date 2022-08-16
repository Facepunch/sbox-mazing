using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Player;
using Sandbox;

namespace Mazing.Enemies;

partial class Jumper : Enemy
{
    public override float MoveSpeed => 85f;
    public TimeSince LastVault { get; set; }
    public Vector3 VaultDir { get; set; }

    public override void Spawn()
    {
        base.Spawn();

        new ModelEntity("models/citizen_clothes/hair/hair_wavyblack/Model/hair_wavyblack.vmdl", this);
        Scale = 0.8f;
        RenderColor = new Color(0f, 0.2f, 0.6f);
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        var controller = (MazingWalkController)Controller;
        if(controller.IsVaulting)
        {
            controller.WishVelocity = VaultDir;
        }
        
        //DebugOverlay.Text((TargetCell - this.GetCellIndex()).Distance.ToString(), EyePosition, 0f, float.MaxValue);
    }

    protected override void OnReachTarget()
    {
        if(LastVault < 2f)
        {
            TargetCell = GetRandomNeighborCell();
            return;
        }

        var cell = this.GetCellIndex();
        var dir = MazeData.Directions.Where(x => Game.IsInMaze(cell + x.Direction))
            .OrderBy(x => Rand.Float() - GetSinceLastVisited(cell + x.Delta) + GetCost(cell + x.Delta))
            .FirstOrDefault();

        TargetCell = this.GetCellIndex() + dir.Direction;

        var controller = (MazingWalkController)Controller;
        if (!controller.IsVaulting && Game.CurrentMaze.GetWall(this.GetCellIndex(), dir.Direction))
        {
            controller.Vault(TargetCell, false);
            LastVault = 0f;
            VaultDir = dir.Delta.Normal;
        }
    }
}