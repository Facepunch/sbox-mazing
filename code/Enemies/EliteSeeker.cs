using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Player;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(20), ThreatValue(3), Replaces(typeof(Seeker), 15)]
partial class EliteSeeker : Enemy
{
    public const float VaultPeriod = 4f;

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
        LastVault = -Rand.Float(2f, 2f + VaultPeriod);
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

        if (player == null)
        {
            TargetCell = GetRandomNeighborCell();
            return;
        }

        var cell = this.GetCellIndex();

        if ( LastVault >= VaultPeriod)
        {
            var baseDist = GetPathLengthTo(player.Position);

            Direction? bestVaultDir = null;
            var bestVaultDist = baseDist;

            foreach (var (dir, delta) in MazeData.Directions)
            {
                if (!Game.CurrentMaze.GetWall(cell, dir)) continue;
                if (!IsInBounds(cell + delta)) continue;

                var dist = GetPathLengthTo(player.Position, cell + delta);

                if (dist < bestVaultDist)
                {
                    bestVaultDist = dist;
                    bestVaultDir = dir;
                }
            }

            if (bestVaultDir != null && Controller is MazingWalkController walkController)
            {
                TargetCell = this.GetCellIndex() + bestVaultDir.Value;

                walkController.Vault(TargetCell, false);

                LastVault = 0f;
                VaultDir = ((GridCoord)bestVaultDir.Value).Normal;

                Sound.FromEntity("player.vault", this);
                return;
            }
        }
        
        TargetCell = GetNextInPathTo(player.Position);
    }
}