using System;
using System.Linq;
using Sandbox;

namespace Mazing;

partial class MazingPlayer : Sandbox.Player
{
    public ClothingContainer Clothing { get; } = new();

    [Net]
    public bool HasExited { get; set; }

    [Net]
    public Key HeldKey { get; set; }

    [Net]
    public TimeSince LastItemDrop { get; set; }

    public MazingPlayer()
    {

    }

    public MazingPlayer( Client cl )
    {
        Clothing.LoadFromClient(cl);
    }

    public override void Respawn()
    {
        SetModel("models/citizen/citizen.vmdl");

        Controller = new MazingWalkController();
        Animator = new MazingPlayerAnimator();
        CameraMode = new MazingCamera();

        Clothing.DressEntity(this);

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        base.Respawn();
    }

    public override void OnKilled()
    {
        base.OnKilled();

        Controller = null;

        EnableAllCollisions = false;
        EnableDrawing = false;
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
    }

    private void CheckForVault()
    {
        if ( !(Controller?.HasEvent( "jump" ) ?? false) ) return;

        Log.Info("Vault event!");

        if (!IsServer)
        {
            return;
        }

        LastItemDrop = 0f;

        if (HeldKey != null)
        {
            var game = MazingGame.Current;

            HeldKey.IsHeld = false;
            HeldKey.Parent = null;
            HeldKey.Position = game.GetCellCenter( HeldKey.Position )
                .WithZ( HeldKey.Position.z );
            HeldKey = null;
        }
    }

    private void CheckForKeyPickup()
    {
        if ( HeldKey != null || HasExited || LastItemDrop < 0.6f )
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
        if ( HeldKey == null || HasExited )
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
        if ( HasExited )
        {
            return;
        }

        if ( Position.z < -128f )
        {
            HasExited = true;
        }
    }
}
