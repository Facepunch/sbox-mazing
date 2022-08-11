using System;
using System.Linq;
using Sandbox;

namespace Mazing;

public static class EntityExtensions
{
    public static (float Row, float Col) GetCell(this Entity entity) => MazingGame.Current.PositionToCell(entity.Position);
    public static GridCoord GetCellIndex(this Entity entity) => MazingGame.Current.PositionToCellIndex(entity.Position);
    public static Direction GetFacingDirection(this Entity entity) => MazeData.GetDirection(entity.EyeRotation.Forward);
}

public partial class MazingPlayer : Sandbox.Player
{
    public ClothingContainer Clothing { get; } = new();

    [Net]
    public bool HasExited { get; set; }

    [Net]
    public bool IsAlive { get; set; }

    public bool IsAliveInMaze => IsAlive && !HasExited;

    [Net]
    public Holdable HeldItem { get; set; }

    [Net]
    public int HeldCoins { get; set; }

    [Net]
    public TimeSince LastItemDrop { get; set; }

    [Net]
    public float VaultCooldown { get; set; } = 3.5f;

    private Particles _sweatParticles;
    private ModelEntity _ragdoll;

    public MazingGame Game => MazingGame.Current;

    public bool CanPickUpItem => HeldItem == null && LastItemDrop > 0.6f;

    public MazingPlayer()
    {

    }

    public MazingPlayer( Client cl )
    {
        Clothing.LoadFromClient(cl);
    }

    public override void Respawn()
    {
        _ragdoll?.Delete();
        _ragdoll = null;

        Tags.Remove( "exited" );
        Tags.Remove( "ghost" );
        Tags.Add( "player" );

        SetModel("models/citizen/citizen.vmdl");

        RenderColor = Color.White;

        Controller = new MazingWalkController
        {
            VaultCooldown = VaultCooldown
        };

        Animator = new MazingPlayerAnimator();
        CameraMode = new MazingCamera();

        Clothing.DressEntity(this);

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        IsAlive = true;

        HeldItem?.SetParent( null );
        HeldItem = null;

        HeldCoins = 0;

        _sweatParticles?.Destroy();
        _sweatParticles = null;

        base.Respawn();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        HeldItem?.SetParent(null);
        HeldItem = null;

        _ragdoll?.Delete();
        _ragdoll = null;
    }

    public void Kill( Vector3 damageDir, bool ragdoll = true )
    {
        if ( !IsServer || !IsAlive )
        {
            return;
        }

        IsAlive = false;

        HeldCoins = 0;

        _sweatParticles?.Destroy();
        _sweatParticles = null;

        DropHeldItem();

        ((MazingWalkController)Controller).VaultCooldown = 0f;

        Tags.Remove( "player" );
        Tags.Add( "ghost" );

        if ( ragdoll )
        {
            _ragdoll = new ModelEntity();

            _ragdoll.SetModel("models/citizen/citizen.vmdl");
            _ragdoll.Position = Position;
            _ragdoll.Rotation = Rotation;
            _ragdoll.SetupPhysicsFromModel(PhysicsMotionType.Dynamic, false);
            _ragdoll.PhysicsGroup.Velocity = damageDir.Normal * 100f;
            _ragdoll.Tags.Add("ragdoll");
        }

        foreach ( var child in Children.ToArray() )
        {
            if ( child is ModelEntity e && e.Tags.Has( "clothes" ) )
            {
                if ( _ragdoll != null )
                {
                    var clothing = new ModelEntity();
                    clothing.CopyFrom(e);
                    clothing.SetParent(_ragdoll, true);
                    clothing.RenderColor = RenderColor;
                }

                e.RenderColor = new Color(1f, 1f, 1f, 0.25f);
            }
        }

        RenderColor = new Color( 1f, 1f, 1f, 0.25f );
    }
    
    public override void Simulate( Client cl )
    {
        base.Simulate(cl);

        if (!IsServer)
            return;
        
    }
    
    [Event.Tick.Server]
    public void ServerTick()
    {
        CheckForVault();
        CheckForItemPickup();
        CheckExited();

        //var cell = Game.GetRandomCell();
        //var cell = Game.GetCellInDirection(this.GetCellIndex(), this.GetFacingDirection(), dist: 2);
        //var color = Game.IsInMaze(cell) ? Color.Cyan : Color.Red;
        //DebugOverlay.Box(Game.CellToPosition(cell), Game.CellToPosition(cell.Row + 1f, cell.Col + 1f), color, depthTest: false);
    }

    [Event.Tick.Client]
    public void ClientTick()
    {

    }

    private void CheckForVault()
    {
        if (!IsServer)
        {
            return;
        }

        if ( Controller?.HasEvent( "vault" ) ?? false )
        {
            if (HeldItem != null)
            {
                var dropCell = this.GetCellIndex() + (GridCoord)this.GetFacingDirection() * 2;
                if (Game.IsInMaze(dropCell))
                    ThrowItem(dropCell);
            }
        }

        if ( (Controller?.HasEvent( "vault_end" ) ?? false) && IsAlive )
        {
            _sweatParticles?.Destroy();
            _sweatParticles = Particles.Create("particles/sweat_drops.vpcf", this, "hat");
        }

        if ( Controller?.HasEvent( "vault_reset" ) ?? false )
        {
            _sweatParticles?.Destroy();
            _sweatParticles = null;
        }

        //DropHeldItem();
    }

    private void DropHeldItem()
    {
        ThrowItem( this.GetCellIndex() );
    }

    public void PickUpItem( Holdable item )
    {
        if ( !CanPickUpItem )
        {
            return;
        }

        HeldItem = item;

        item.Parent = this;
        item.TargetPosition = Vector3.Up * 64f + Vector3.Forward * 8f;
    }

    private void ThrowItem( GridCoord cell )
    {
        if ( HeldItem == null ) return;

        LastItemDrop = 0f;

        HeldItem.Parent = null;
        HeldItem.TargetPosition = Game.CellCenterToPosition( cell );
        HeldItem = null;
    }

    private void CheckForItemPickup()
    {
        if ( !IsAliveInMaze )
        {
            return;
        }

        foreach ( var treasure in Game.Treasure)
        {
            var diff = treasure.Position.WithZ( 0 ) - Position;

            if ( diff.LengthSquared < 20f * 20f )
            {
                HeldCoins += treasure.Value;

                treasure.Delete();
                break;
            }
        }
    }

    private void CheckExited()
    {
        if ( !IsAliveInMaze || Controller is MazingWalkController walkController && walkController.IsVaulting )
        {
            return;
        }

        var exitCell = Game.ExitCell;

        if ( this.GetCellIndex() != exitCell )
        {
            return;
        }

        if ( Game.Hatch == null || !Game.Hatch.IsOpen )
        {
            return;
        }

        HasExited = true;
        EnableAllCollisions = false;

        Game.TotalCoins += HeldCoins;
        HeldCoins = 0;

        Tags.Remove( "player" );
        Tags.Add( "exited" );
    }
}
