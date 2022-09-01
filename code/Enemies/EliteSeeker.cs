using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(20), ThreatValue(3), Replaces(typeof(Seeker), 15)]
partial class EliteSeeker : Enemy
{
    public override string NounPhrase => "an Elite Seeker";
    public override float MoveSpeed => 80.6f;
    public TimeSince LastVault { get; set; }
    public Vector3 VaultDir { get; set; }

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
        AddClothingItem("models/citizen_clothes/skin01.clothing");
        AddClothingItem("models/citizen_clothes/trousers/LegArmour/leg_armour.clothing");
        AddClothingItem("models/citizen_clothes/shoes/Boots/army_boots.clothing");
        Clothing.DressEntity(this);

        RenderColor = new Color(1f, 0.3f, 0.7f, 1f);

        foreach (var child in Children.ToArray())
        {
            if (child is ModelEntity e && e.Tags.Has("clothes"))
            {
                e.RenderColor = new Color(1f, 0f, 0f, 0f);
            }
        }

        Scale = 1f;
    }

    //protected override void OnServerTick()
    //{
    //    base.OnServerTick();

    //    var player = Game.GetClosestPlayer(Position);
    //    if (player != null)
    //    {
    //        DebugOverlay.Text(GetPathLengthTo(player.Position).ToString() + "\n" + (player.Position.WithZ(0) - Position.WithZ(0)).Length + " \n" + (this.GetCellIndex() - player.GetCellIndex()).Distance.ToString() + " \n" + MazeData.GetDirection(player.Position.WithZ(0) - Position.WithZ(0)).ToString(), EyePosition, 0f, float.MaxValue);
    //    }
    //}

    protected override void OnReachTarget()
    {
        var player = Game.GetClosestPlayer(Position);

        if (LastVault > 3f && player != null && (player.Position.WithZ(0) - Position.WithZ(0)).Length < 95f && GetPathLengthTo(player.Position) > 3)
        //if (LastVault > 3f && player != null && (this.GetCellIndex() - player.GetCellIndex()).Distance == 1 && GetPathLengthTo(player.Position) > 3)
        {
            var cell = this.GetCellIndex();
            var dir = MazeData.GetDirection(player.Position.WithZ(0) - Position.WithZ(0));
            TargetCell = this.GetCellIndex() + dir;

            var controller = (Mazing.Player.MazingWalkController)Controller;
            if (!controller.IsVaulting && Game.CurrentMaze.GetWall(this.GetCellIndex(), dir))
            {
                controller.Vault(TargetCell, false);
                LastVault = 0f;
                VaultDir = MazeData.Directions[(int)dir].Delta.Normal;

                Sound.FromEntity("player.vault", this);
            }

            return;
        }

        if (player == null)
        {
            TargetCell = GetRandomNeighborCell();
        }
        else
        {
            TargetCell = GetNextInPathTo(player.Position);
        }
    }
}