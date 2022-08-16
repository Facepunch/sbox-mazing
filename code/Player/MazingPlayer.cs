using System;
using System.Linq;
using Mazing.Items;
using Mazing.UI;
using Sandbox;
using Sandbox.UI;

namespace Mazing.Player;

public partial class MazingPlayer : Sandbox.Player, IHoldable
{
    public ClothingContainer Clothing { get; } = new();

    [Net]
    public bool HasExited { get; set; }

    [Net]
    public TimeSince ExitTime { get; set; }

    [Net]
    public bool IsAlive { get; set; }

    public bool IsAliveInMaze => IsAlive && !HasExited;

    [Net]
    public IHoldable HeldEntity { get; set; }

    [Net, HideInEditor]
    public int HeldCoins { get; private set; }

    [Net, HideInEditor]
    public int SurvivalStreak { get; set; }

    [Net]
    public TimeSince LastItemDrop { get; set; }

    [Net]
    public float VaultCooldown { get; set; } = 3.5f;

    public NameplateRoot Nameplate { get; private set; }

    public bool IsVaulting => Controller is MazingWalkController controller && controller.IsVaulting;

    private Particles _sweatParticles;
    private ModelEntity _ragdoll;

    public MazingGame Game => MazingGame.Current;

    public bool CanPickUpItem => HeldEntity == null || HeldEntity is MazingPlayer && LastItemDrop > 0.6f && !IsVaulting;

    public MazingPlayer()
    {

    }

    public MazingPlayer( Client cl )
    {
        Clothing.LoadFromClient(cl);
    }

    public override void ClientSpawn()
    {
        base.ClientSpawn();

        Nameplate = new NameplateRoot( this );
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

        UsePhysicsCollision = false;

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        IsAlive = true;

        HeldEntity?.OnThrown( this.GetCellIndex() );
        HeldEntity = null;

        HeldCoins = 0;
        SurvivalStreak = Math.Min( SurvivalStreak, Game.LevelIndex );

        _sweatParticles?.Destroy();
        _sweatParticles = null;

        base.Respawn();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        HeldEntity?.OnThrown( this.GetCellIndex() );
        HeldEntity = null;

        _ragdoll?.Delete();
        _ragdoll = null;

        Nameplate?.Delete();
        Nameplate = null;
    }

    private TimeSince _lastFootstep;

    public override void OnAnimEventFootstep( Vector3 pos, int foot, float volume )
    {
        base.OnAnimEventFootstep( pos, foot, volume );

        if ( GroundEntity != null && IsClient && _lastFootstep > 0.25f )
        {
            _lastFootstep = 0f;
            var sound = Sound.FromWorld("player.footstep", pos);
            sound.SetVolume( Math.Clamp( Velocity.Length / 160f, 0f, 1f ) );
        }
    }

    [ClientRpc]
    public static void ClientDeathNotify( string name, string message, int coins, int streak )
    {
        if ( coins == 0 )
        {
            if ( streak >= 5 )
            {
                ChatBox.AddInformation($"{message}, losing a {streak} level streak!");
            }
            else
            {
                ChatBox.AddInformation($"{message}!");
            }
        }
        else
        {
            if (streak >= 5)
            {
                ChatBox.AddInformation($"{message}, losing a {streak} level streak and ${coins}!");
            }
            else
            {
                ChatBox.AddInformation($"{message}, losing ${coins}!");
            }
        }
    }

    [ClientRpc]
    public static void ClientExitNotify( string name, string message, int coins )
    {
        if (coins == 0)
        {
            ChatBox.AddInformation($"{message}!");
        }
        else
        {
            ChatBox.AddInformation($"{message}, banking ${coins}!");
        }
    }

    public void Kill( Vector3 damageDir, string message, bool ragdoll = true )
    {
        if ( !IsServer || !IsAlive )
        {
            return;
        }

        ClientDeathNotify( Client.Name, string.Format( message, Client.Name ), HeldCoins, SurvivalStreak );

        IsAlive = false;

        HeldCoins = 0;
        SurvivalStreak = 0;

        _sweatParticles?.Destroy();
        _sweatParticles = null;

        if ( Parent is MazingPlayer holder )
        {
            holder.DropHeldItem();
        }

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

    public void AddCoins( int value, bool applyBonus = true )
    {
        if ( applyBonus )
        {
            value = value * (5 + Math.Max( 0, SurvivalStreak )) / 5;
        }

        HeldCoins += value;
    }
    
    [Event.Tick.Server]
    public void ServerTick()
    {
        CheckForVault();
        CheckExited();

        if ( HasExited && ExitTime > 0.5f )
        {
            RenderColor = RenderColor.WithAlpha( Math.Clamp( 1f - (ExitTime - 0.5f) * 2f, 0f, 1f ) );

            foreach (var child in Children.ToArray())
            {
                if ( child is ModelEntity e )
                {
                    e.RenderColor = RenderColor;
                }
            }
        }

        //var cell = Game.GetRandomCell();
        //var cell = Game.GetCellInDirection(this.GetCellIndex(), this.GetFacingDirection(), dist: 2);
        //var color = Game.IsInMaze(cell) ? Color.Cyan : Color.Red;
        //DebugOverlay.Box(Game.CellToPosition(cell), Game.CellToPosition(cell.Row + 1f, cell.Col + 1f), color, depthTest: false);
    }

    [Event.Tick.Client]
    public void ClientTick()
    {

    }

    public void OnVault()
    {
        if (HeldEntity != null)
        {
            var dropCell = this.GetCellIndex() + (GridCoord)this.GetFacingDirection() * 2;
            if (Game.IsInMaze(dropCell))
                ThrowItem(dropCell);
        }
    }

    private void CheckForVault()
    {
        if (!IsServer)
        {
            return;
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

    public void PickUp( IHoldable holdable )
    {
        if ( HeldEntity is MazingPlayer heldPlayer )
        {
            heldPlayer.PickUp( holdable );
            return;
        }

        if (holdable is not MazingPlayer && !CanPickUpItem)
        {
            return;
        }

        if ( holdable is MazingPlayer player && HeldEntity != null )
        {
            player.PickUp(HeldEntity);

            HeldEntity = player;
            HeldEntity.OnPickedUp( this );
        }
        else
        {
            HeldEntity = holdable;
            HeldEntity.OnPickedUp( this );
        }
    }

    public void ThrowItem( GridCoord cell )
    {
        if ( HeldEntity == null ) return;

        LastItemDrop = 0f;
        
        HeldEntity.OnThrown( cell );
        HeldEntity = null;
    }

    private void CheckExited()
    {
        if ( !IsAliveInMaze || Controller is MazingWalkController walkController && walkController.IsVaulting || Parent is MazingPlayer )
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
        ExitTime = 0f;
        EnableAllCollisions = false;

        DropHeldItem();

        ClientExitNotify( Client.Name, $"{Client.Name} has escaped", HeldCoins );

        if ( Game.PlayersAliveInMaze.Any() )
        {
            Sound.FromScreen( "player.escape" );
        }
        else
        {
            Sound.FromScreen( "player.victory" );
        }

        Game.TotalCoins += HeldCoins;

        HeldCoins = 0;
        SurvivalStreak += 1;

        Tags.Remove( "player" );
        Tags.Add( "exited" );
    }

    public void OnPickedUp( MazingPlayer holder )
    {
        Parent = holder;

        EnableAllCollisions = false;
    }

    public void OnThrown( GridCoord target )
    {
        Parent = null;

        EnableAllCollisions = true;

        if (HeldEntity != null)
        {
            var nextCell = target + this.GetFacingDirection();

            if (Game.IsInMaze(nextCell))
            {
                ThrowItem(nextCell);
            }
        }

        (Controller as MazingWalkController)?.Vault( target );
    }
}
