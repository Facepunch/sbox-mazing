﻿using System;
using System.Linq;
using Sandbox;

namespace Mazing;

public static class EntityExtensions
{
    public static (float Row, float Col) GetCell(this Entity entity) => MazingGame.Current.PositionToCell(entity.Position);
    public static GridCoord GetCellIndex(this Entity entity) => MazingGame.Current.PositionToCellIndex(entity.Position);
    public static Direction GetFacingDirection(this Entity entity) => MazeData.GetDirection(entity.EyeRotation.Forward);
}

partial class MazingPlayer : Sandbox.Player
{
    public ClothingContainer Clothing { get; } = new();

    [Net]
    public bool HasExited { get; set; }

    [Net]
    public bool IsAlive { get; set; }

    public bool IsAliveInMaze => IsAlive && !HasExited;

    [Net]
    public Key HeldKey { get; set; }

    [Net]
    public TimeSince LastItemDrop { get; set; }

    private ModelEntity _ragdoll;

    public MazingGame Game => MazingGame.Current;

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

        Tags.Remove( "ghost" );
        Tags.Add( "player" );

        SetModel("models/citizen/citizen.vmdl");

        RenderColor = Color.White;

        Controller = new MazingWalkController();
        Animator = new MazingPlayerAnimator();
        CameraMode = new MazingCamera();

        Clothing.DressEntity(this);

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        IsAlive = true;

        base.Respawn();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _ragdoll?.Delete();
        _ragdoll = null;
    }

    public void Kill( Vector3 damageDir )
    {
        if ( !IsServer || !IsAlive )
        {
            return;
        }

        IsAlive = false;

        DropHeldItem();

        Tags.Remove( "player" );
        Tags.Add( "ghost" );

        _ragdoll = new ModelEntity();

        _ragdoll.SetModel( "models/citizen/citizen.vmdl" );
        _ragdoll.Position = Position;
        _ragdoll.Rotation = Rotation;
        _ragdoll.SetupPhysicsFromModel( PhysicsMotionType.Dynamic, false );
        _ragdoll.PhysicsGroup.Velocity = damageDir.Normal * 100f;
        _ragdoll.Tags.Add( "ragdoll" );

        foreach ( var child in Children.ToArray() )
        {
            if ( child is ModelEntity e && e.Tags.Has( "clothes" ) )
            {
                var clothing = new ModelEntity();
                clothing.CopyFrom( e );
                clothing.SetParent( _ragdoll, true );
                clothing.RenderColor = RenderColor;

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
        CheckForKeyPickup();
        CheckForLockOpen();
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
        if ( !(Controller?.HasEvent( "jump" ) ?? false) ) return;

        Log.Info("Vault event!");

        if (!IsServer)
        {
            return;
        }

        if (HeldKey != null)
        {
            var dropCell = this.GetCellIndex() + (GridCoord) this.GetFacingDirection() * 2;
            if (Game.IsInMaze(dropCell))
                ThrowItem(dropCell);
        }

        //DropHeldItem();
    }

    private void DropHeldItem()
    {
        LastItemDrop = 0f;

        if (HeldKey != null)
        {
            var game = MazingGame.Current;

            HeldKey.IsHeld = false;
            HeldKey.Parent = null;
            HeldKey.Position = game.GetCellCenter(HeldKey.Position)
                .WithZ(HeldKey.Position.z);
            HeldKey = null;
        }
    }

    private void ThrowItem(GridCoord cell)
    {
        if (HeldKey != null)
        {
            HeldKey.IsHeld = false;
            HeldKey.Parent = null;
            HeldKey.Position = Game.CellCenterToPosition(cell).WithZ(HeldKey.Position.z);
            HeldKey = null;
        }
    }

    private void CheckForKeyPickup()
    {
        if ( HeldKey != null || !IsAliveInMaze || LastItemDrop < 0.6f )
        {
            return;
        }

        var keys = Entity.All.OfType<Key>();

        foreach (var key in keys)
        {
            if ( key.IsHeld )
            {
                continue;
            }

            var diff = key.Position.WithZ(0) - Position.WithZ(0);

            if (diff.LengthSquared < 20f * 20f)
            {
                HeldKey = key;
                key.IsHeld = true;

                key.Parent = this;
                key.LocalPosition = Vector3.Up * 64f;

                break;
            }
        }
    }

    private void CheckForLockOpen()
    {
        if ( HeldKey == null || !IsAliveInMaze )
        {
            return;
        }

        var hatches = Entity.All.OfType<Hatch>();

        foreach ( var hatch in hatches )
        {
            if ( hatch.IsOpen )
            {
                continue;
            }

            var diff = hatch.Position.WithZ(0) - Position.WithZ(0);

            if (diff.LengthSquared < 20f * 20f)
            {
                HeldKey.Delete();
                HeldKey = null;

                hatch.Open();
                break;
            }
        }
    }

    private void CheckExited()
    {
        if ( !IsAliveInMaze )
        {
            return;
        }

        if ( Position.z < -128f )
        {
            HasExited = true;
        }
    }
}
