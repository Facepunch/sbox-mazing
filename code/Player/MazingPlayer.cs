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

    public bool IsHeavy => true;

    public bool IsAliveInMaze => IsAlive && !HasExited;

    public bool IsSpawning { get; set; }

    [Net]
    public Entity HeldEntityNetworked { get; set; }

    public IHoldable HeldEntity
    {
        get => HeldEntityNetworked as IHoldable;
        set => HeldEntityNetworked = value as Entity;
    }

    [Net]
    public MazingPlayer ParentPlayer { get; set; }

    [Net, HideInEditor]
    public int HeldCoins { get; private set; }

    [Net, HideInEditor]
    public int SurvivalStreak { get; set; }

    public const int MaxBonusSurvivalStreak = 5;

    [Net]
    public TimeSince LastItemDrop { get; set; }

    [Net]
    public float VaultCooldown { get; set; } = 3.5f;

    [HideInEditor]
    public int FirstSeenLevelIndex { get; set; }

    [Net]
    public Entity SoundEmitter { get; set; }

    public NameplateRoot Nameplate { get; private set; }

    public bool IsVaulting => Controller is MazingWalkController controller && controller.IsVaulting;

    [Net]
    public bool IsSpectatorOnly { get; set; }

    private Particles _sweatParticles;
    private Particles _burstParticles;
    private AnimatedEntity _ragdoll;

    public MazingGame Game => MazingGame.Current;

    public bool CanPickUpItem => HeldEntity == null || HeldEntity is MazingPlayer && LastItemDrop > 0.6f && !IsVaulting;

    public MazingPlayer()
    {

    }

    public MazingPlayer( IClient cl )
    {
        Clothing.LoadFromClient(cl);

        SoundEmitter = new Entity();
        SoundEmitter.SetParent( this, "eyes", Transform.Zero );
    }

    public Sound EmitSound( string name )
    {
        return Sound.FromEntity( name, this );
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

        Clothing.DressEntity(this);

        UsePhysicsCollision = false;

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        IsAlive = true;
        IsSpawning = true;

        HeldEntity?.OnThrown( this.GetCellIndex(), this.GetFacingDirection() );
        HeldEntity = null;

        HeldCoins = 0;
        SurvivalStreak = Math.Min( SurvivalStreak, Game.LevelIndex );

        _sweatParticles?.Destroy();
        _sweatParticles = null;

        base.Respawn();
    }

    public override void CreateHull()
    {
        var controller = (MazingWalkController)Controller;

        var diag = MathF.Sqrt( 0.5f ) * controller.BodyGirth * 0.5f;
        
        SetupPhysicsFromAABB(PhysicsMotionType.Keyframed, new Vector3(-diag, -diag, 0), new Vector3(diag, diag, controller.BodyHeight));

        // var capsule = Capsule.FromHeightAndRadius( controller.BodyHeight, controller.BodyGirth * 0.5f );
        // var phys = SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, capsule );

        // TODO - investigate this? if we don't set movetype then the lerp is too much. Can we control lerp amount?
        // if so we should expose that instead, that would be awesome.
        EnableHitboxes = true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        HeldEntity?.OnThrown( this.GetCellIndex(), this.GetFacingDirection() );
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

        if ( GroundEntity != null && Sandbox.Game.IsClient && _lastFootstep > 0.25f )
        {
            _lastFootstep = 0f;
            var sound = Sound.FromWorld("player.footstep", pos);
            sound.SetVolume( Math.Clamp( Velocity.Length / 160f, 0f, 1f ) );
        }
    }

    [ClientRpc]
    public static void ClientDeathNotify( long playerId, string message, int coins, int streak )
    {
        if ( coins == 0 )
        {
            ChatBox.AddInformation(
                streak >= 5
                    ? $"{message}, losing a {streak} level streak!"
                    : $"{message}!",
                avatar: $"avatar:{playerId}" );
        }
        else
        {
            ChatBox.AddInformation(
                streak >= 5
                    ? $"{message}, losing a {streak} level streak and ${coins}!"
                    : $"{message}, losing ${coins}!",
                avatar: $"avatar:{playerId}" );
        }
    }

    [ClientRpc]
    public static void ClientExitNotify( long playerId, string message, int coins )
    {
        if (coins == 0)
        {
            ChatBox.AddInformation($"{message}!",
                avatar: $"avatar:{playerId}");
        }
        else
        {
            ChatBox.AddInformation($"{message}, banking ${coins}!",
                avatar: $"avatar:{playerId}");
        }
    }

    public void Kill( Vector3 damageDir, string message, Entity attacker, bool ragdoll = true )
    {
        if ( !Sandbox.Game.IsServer || !IsAlive )
        {
            return;
        }

        ClientDeathNotify( Client.SteamId, string.Format( message, Client.Name ), HeldCoins, SurvivalStreak );

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
            _ragdoll = new AnimatedEntity();

            _ragdoll.SetModel("models/citizen/citizen.vmdl");
            _ragdoll.Position = Position;
            _ragdoll.Rotation = Rotation;
            _ragdoll.SetupPhysicsFromModel(PhysicsMotionType.Dynamic, false);
            _ragdoll.PhysicsGroup.Velocity = damageDir.Normal * 100f;
            _ragdoll.Tags.Add("ragdoll");
            
            Clothing.DressEntity(_ragdoll);
        }

        foreach ( var child in Children.ToArray() )
        {
            if ( child is ModelEntity e && child.Tags.Has("clothes") )
            {
                e.RenderColor = new Color(1f, 1f, 1f, 0.25f);
            }
        }

        RenderColor = new Color( 1f, 1f, 1f, 0.25f );
    }

    public float GetSurvivalStreakBonus()
    {
        var streak = Math.Min( MaxBonusSurvivalStreak, SurvivalStreak );

        return streak < 1 ? 1f : streak < 2 ? 2f : streak;
    }

    public Color GetSurvivalStreakColor()
    {
        return SurvivalStreak < MaxBonusSurvivalStreak ? Color.White : Color.FromRgb( 0xFFD700 );
    }

    public void AddCoins( int value, bool applyBonus = true )
    {
        if ( applyBonus )
        {
            value = (int) MathF.Round( GetSurvivalStreakBonus() * value );
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
        CheckForVault();
    }

    public void OnVault()
    {
        if (HeldEntity != null)
        {
            var dropCell = this.GetCellIndex() + (GridCoord)this.GetFacingDirection() * 2;
            if ( Game.IsInMaze( dropCell ) )
                ThrowItem( dropCell, this.GetFacingDirection() );
        }

        EmitSound( "player.vault" );
    }

    private float _lastUntilVault;

    private void CheckForVault()
    {
        if (Controller is not MazingWalkController walkController)
        {
            return;
        }

        var untilNextVault = walkController.IsVaulting && walkController.UntilNextVault >= 3f ? 0f : walkController.UntilNextVault;

        //if ( IsClient && Client.IsOwnedByLocalClient )
        if ( Sandbox.Game.IsServer && IsAliveInMaze && untilNextVault > -0.5f )
        {
            if ( _lastUntilVault > 0f && untilNextVault <= 0f )
            {
                Sound.FromScreen( To.Single( Client ), "player.recharge2" );
            }
            else if ( _lastUntilVault > 1f && untilNextVault <= 1f )
            {
                Sound.FromScreen( To.Single( Client ), "player.recharge1" );
            }
            else if ( _lastUntilVault > 2f && untilNextVault <= 2f )
            {
                Sound.FromScreen( To.Single( Client ), "player.recharge1quiet" );
            }
        }

        _lastUntilVault = untilNextVault;

        if ( Sandbox.Game.IsServer )
        {
            if (IsAliveInMaze && _sweatParticles == null && walkController.IsVaultOnCooldown)
            {
                _sweatParticles = Particles.Create("particles/sweat_drops.vpcf", this, "hat");
            }

            if (_sweatParticles != null && (!walkController.IsVaultOnCooldown || !IsAliveInMaze))
            {
                _sweatParticles?.Destroy();
                _sweatParticles = null;

                _burstParticles?.Destroy();
                _burstParticles = Particles.Create("particles/sweat_burst.vpcf", this, "hat");

                EmitSound("player.recharge");
            }
        }
    }

    private void DropHeldItem()
    {
        ThrowItem( this.GetCellIndex(), this.GetFacingDirection() );
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

    public void ThrowItem( GridCoord cell, Direction direction )
    {
        if ( HeldEntity == null ) return;

        LastItemDrop = 0f;

        HeldEntity.OnThrown( cell, direction );
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

        ClientExitNotify( Client.SteamId, $"{Client.Name} has escaped", HeldCoins );

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
        Parent = ParentPlayer = holder;

        EnableAllCollisions = false;
    }

    public void OnThrown( GridCoord target, Direction direction )
    {
        Parent = ParentPlayer = null;

        EnableAllCollisions = true;

        if (HeldEntity != null)
        {
            var nextCell = target + direction;

            if ( Game.IsInMaze( nextCell ) )
            {
                ThrowItem( nextCell, direction );
            }
        }

        (Controller as MazingWalkController)?.Vault( target, false );
    }

    public override void FrameSimulate( IClient cl )
    {
        FrameSimulateCamera( cl );
    }
}
