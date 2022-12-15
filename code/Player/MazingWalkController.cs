using System;
using System.Collections.Generic;
using System.Linq;
using Mazing.Enemies;
using Mazing.Items;
using Sandbox;

namespace Mazing.Player;

public partial class MazingWalkController : BasePlayerController
{
    [Net] public float DefaultSpeed { get; set; } = 160f;
    [Net] public float Acceleration { get; set; } = 10.0f;
    [Net] public float AirAcceleration { get; set; } = 50.0f;
    [Net] public float GroundFriction { get; set; } = 8.0f;
    [Net] public float StopSpeed { get; set; } = 100.0f;
    [Net] public float GroundAngle { get; set; } = 46.0f;
    [Net] public float StepSize { get; set; } = 8.0f;
    [Net] public float MaxNonJumpVelocity { get; set; } = 140.0f;
    [Net] public float BodyGirth { get; set; } = 38.0f;
    [Net] public float BodyHeight { get; set; } = 72.0f;
    [Net] public float EyeHeight { get; set; } = 64.0f;
    [Net] public float BaseGravity { get; set; } = 800.0f;
    public float Gravity => IsGhost ? 0f : BaseGravity;
    [Net] public float AirControl { get; set; } = 30.0f;
    [Net] public float VaultTime { get; set; } = 0.6f;
    //[Net] public float PostVaultTime { get; set; } = 0.2f;
    [Net] public float VaultHeight { get; set; } = 192f;
    [Net] public float VaultCooldown { get; set; } = 3.5f;
    [Net] public float GhostHeight { get; set; } = 384f;

    public bool IsGhost => Pawn is MazingPlayer player && !player.IsAlive;

    public bool IsVaulting => SinceVault <= VaultTime && Pawn?.Parent is not MazingPlayer;
    public bool IsVaultOnCooldown => NextVault <= 0f || _localSinceVault <= VaultTime;

    public float UntilNextVault => Math.Max( -NextVault, VaultTime - _localSinceVault );

    public bool IsPlayer => Pawn is MazingPlayer;
    public bool IsBot => Pawn is MazingPlayer player && player.Client.IsBot != Input.Down( InputButton.Walk );

    public Vector3 InputVector { get; set; }

    [Net, Predicted]
    public Vector3 VaultOrigin { get; set; }

    [Net, Predicted]
    public Vector3 VaultTarget { get; set; }

    [Net, Predicted]
    public TimeSince SinceVault { get; set; }

    [Net, Predicted]
    public TimeSince NextVault { get; set; }

    private TimeSince _localSinceVault;

    public Unstuck Unstuck;

    public MazingWalkController()
    {
        Unstuck = new Unstuck( this );
        SinceVault = float.MaxValue;
        _localSinceVault = float.MaxValue;
    }

    /// <summary>
    /// This is temporary, get the hull size for the player's collision
    /// </summary>
    public override BBox GetHull()
    {
        var girth = BodyGirth * 0.5f;
        var mins = new Vector3( -girth, -girth, 0 );
        var maxs = new Vector3( +girth, +girth, BodyHeight );

        return new BBox( mins, maxs );
    }

    // Duck body height 32
    // Eye Height 64
    // Duck Eye Height 28

    protected Vector3 mins;
    protected Vector3 maxs;

    public virtual void SetBBox( Vector3 mins, Vector3 maxs )
    {
        if ( this.mins == mins && this.maxs == maxs )
            return;

        this.mins = mins;
        this.maxs = maxs;
    }

    /// <summary>
    /// Update the size of the bbox. We should really trigger some shit if this changes.
    /// </summary>
    public virtual void UpdateBBox()
    {
        var girth = BodyGirth * 0.5f;

        var mins = new Vector3( -girth, -girth, 0 ) * Pawn.Scale;
        var maxs = new Vector3( +girth, +girth, BodyHeight ) * Pawn.Scale;

        SetBBox( mins, maxs );
    }

    protected float SurfaceFriction;

    private void UpdateEyeRotation()
    {
        if ( IsVaulting || Pawn is Enemy enemy && !enemy.IsAwake )
        {
            return;
        }

        var dir = (InputVector + WishVelocity).WithZ( 0f );

        if ( dir.LengthSquared < 0.25f * 0.25f )
        {
            return;
        }

        if ( Math.Abs( dir.x ) > Math.Abs( dir.y ) )
        {
            dir.y = 0f;
        }
        else
        {
            dir.x = 0f;
        }

        EyeRotation = Rotation.LookAt( dir, Vector3.Up );
    }


    public override void FrameSimulate()
    {
        base.FrameSimulate();

        UpdateEyeRotation();
    }

    public void KeepInBounds()
    {
        var game = MazingGame.Current;
        var a = game.CellCenterToPosition( (0, 0) );
        var b = game.CellCenterToPosition( (game.CurrentMaze.Rows - 1, game.CurrentMaze.Cols - 1) );

        var min = Vector3.Min( a, b );
        var max = Vector3.Max( a, b );

        if ( Position.x < min.x )
        {
            Position = Position.WithX( min.x );
            WishVelocity = WishVelocity.WithX( Math.Max( 0f, WishVelocity.x ) );
            Velocity = Velocity.WithX( Math.Max( 0f, Velocity.x ) );
        }

        if ( Position.x > max.x )
        {
            Position = Position.WithX( max.x );
            WishVelocity = WishVelocity.WithX( Math.Min( 0f, WishVelocity.x ) );
            Velocity = Velocity.WithX( Math.Min( 0f, Velocity.x ) );
        }

        if ( Position.y < min.y )
        {
            Position = Position.WithY( min.y );
            WishVelocity = WishVelocity.WithY( Math.Max( 0f, WishVelocity.y ) );
            Velocity = Velocity.WithY( Math.Max( 0f, Velocity.y ) );
        }

        if ( Position.y > max.y )
        {
            Position = Position.WithY( max.y );
            WishVelocity = WishVelocity.WithY( Math.Min( 0f, WishVelocity.y ) );
            Velocity = Velocity.WithY( Math.Min( 0f, Velocity.y ) );
        }
    }

    private void SimulatePhysics()
    {
        EyeLocalPosition = Vector3.Up * (EyeHeight * Pawn.Scale);
        UpdateBBox();

        EyeLocalPosition += TraceOffset;

        if ( Pawn is MazingPlayer pawnPlayer && pawnPlayer.ParentPlayer != null )
        {
            SetTag( "held" );

            Position = pawnPlayer.ParentPlayer.Position + Vector3.Up * pawnPlayer.ParentPlayer.WorldSpaceBounds.Size.z;
            Rotation = pawnPlayer.ParentPlayer.Rotation;
            EyeRotation = pawnPlayer.ParentPlayer.Rotation;

            if ( NextVault > 0f && !IsBot && Input.Down( InputButton.Jump ) )
            {
                var cell = Pawn.GetCellIndex();
                var inputDir = new Vector3( -pawnPlayer.InputDirection.y, pawnPlayer.InputDirection.x, 0 );

                if ( inputDir.Length < 0.25f )
                {
                    inputDir = EyeRotation.Forward;
                }

                var dir = MazeData.GetDirection( inputDir );
                var target = cell + dir;

                if ( MazingGame.Current.IsInMaze( target ) )
                {
                    if ( Game.IsServer )
                    {
                        pawnPlayer.ParentPlayer.ThrowItem( target, dir );
                    }

                    _localSinceVault = 0f;
                    NextVault = -VaultCooldown;
                }
            }

            return;
        }

        RestoreGroundPos();

        if ( !IsVaulting && Unstuck.TestAndFix() )
            return;

        if ( MazingGame.Current == null )
            return;

        //
        // Start Gravity
        //
        Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;
        Velocity += new Vector3( 0, 0, BaseVelocity.z ) * Time.Delta;

        WishVelocity = default;

        BaseVelocity = BaseVelocity.WithZ( 0 );

        if ( IsVaulting )
        {
            VaultMove();
        }
        else
        {
            var game = MazingGame.Current;
            var (rowF, colF) = game.PositionToCell( Position );
            var cell = new GridCoord( rowF.FloorToInt(), colF.FloorToInt() );

            if ( Debug )
            {
                DebugOverlay.Box( game.CellToPosition( cell ), game.CellToPosition( cell.Row + 1f, cell.Col + 1f ),
                    new Color( 0.5f, 0.5f, 0.5f, 1f ), depthTest: false );
            }

            if ( Pawn is MazingPlayer player )
            {
                if ( player.HasExited )
                {
                    WishVelocity = game.CellCenterToPosition( game.ExitCell ) - player.Position;
                }
                else if ( !IsBot )
                {
                    WishVelocity = InputVector = new Vector3( -player.InputDirection.y, player.InputDirection.x, 0 );
                }
            }
            else
            {
                WishVelocity = InputVector;
            }

            //
            // Work out wish velocity.. just take input, rotate it to view, clamp to -1, 1
            //
            var inSpeed = WishVelocity.Length.Clamp( 0, 1 );

            WishVelocity = WishVelocity.WithZ( 0 );

            UpdateEyeRotation();

            if ( !IsGhost )
            {
                WallGapAssist();
            }

            WishVelocity = WishVelocity.Normal * inSpeed;
            WishVelocity *= GetWishSpeed();

            if ( !IsGhost )
            {
                if ( !IsVaultOnCooldown && IsPlayer && ((MazingPlayer) Pawn).IsAliveInMaze && !IsBot && !((MazingPlayer) Pawn).IsSpawning && Input.Down( InputButton.Jump ) )
                {
                    var dir = Pawn.GetFacingDirection();
                    var next = cell + dir;

                    if ( game.CurrentMaze.GetWall( cell, dir ) && game.IsInMaze( next ) )
                    {
                        Vault( next, true );
                    }
                }
            }
            else
            {
                KeepInBounds();
            }

            // Fricion is handled before we add in any base velocity. That way, if we are on a conveyor,
            //  we don't slow when standing still, relative to the conveyor.
            bool bStartOnGround = GroundEntity != null;
            //bool bDropSound = false;
            if ( bStartOnGround || IsGhost )
            {
                //if ( Velocity.z < FallSoundZ ) bDropSound = true;

                Velocity = Velocity.WithZ( 0 );
                //player->m_Local.m_flFallVelocity = 0.0f;

                if ( GroundEntity != null || IsGhost )
                {
                    ApplyFriction( GroundFriction * SurfaceFriction );
                }
            }

            if ( IsGhost )
            {
                Velocity += Vector3.Up * (GhostHeight - Position.z);
                Position = Position.WithZ( Math.Clamp( Position.z + Velocity.z * Time.Delta, 32f, GhostHeight + 64f ) );
            }

            bool bStayOnGround = false;
            if ( GroundEntity != null || IsGhost )
            {
                bStayOnGround = true;
                WalkMove();
            }
            else
            {
                WishVelocity = Vector3.Zero;
                AirMove();
            }

            CategorizePosition( bStayOnGround );
        }

        // FinishGravity
        Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;

        if ( GroundEntity != null && !IsGhost )
        {
            Velocity = Velocity.WithZ( 0 );
        }

        SaveGroundPos();

        if ( Debug )
        {
            DebugOverlay.Box( Position + TraceOffset, mins, maxs, Color.Red );
            DebugOverlay.Box( Position, mins, maxs, Color.Blue );

            var lineOffset = 0;
            if ( Game.IsServer ) lineOffset = 10;

            DebugOverlay.ScreenText( $"        Position: {Position}", lineOffset + 0 );
            DebugOverlay.ScreenText( $"        Velocity: {Velocity}", lineOffset + 1 );
            DebugOverlay.ScreenText( $"    BaseVelocity: {BaseVelocity}", lineOffset + 2 );
            DebugOverlay.ScreenText( $"    GroundEntity: {GroundEntity} [{GroundEntity?.Velocity}]", lineOffset + 3 );
            DebugOverlay.ScreenText( $" SurfaceFriction: {SurfaceFriction}", lineOffset + 4 );
            DebugOverlay.ScreenText( $"    WishVelocity: {WishVelocity}", lineOffset + 5 );
        }
    }

    private void SimulateAnimation()
    {
        // where should we be rotated to
        var turnSpeed = 0.02f;

        var rotation = EyeRotation;

        var idealRotation = Rotation.LookAt( rotation.Forward.WithZ( 0 ), Vector3.Up );
        Rotation = Rotation.Slerp( Rotation, idealRotation, WishVelocity.Length * Time.Delta * turnSpeed );
        Rotation = Rotation.Clamp( idealRotation, 45.0f, out var shuffle ); // lock facing to within 45 degrees of look direction

        CitizenAnimationHelper animHelper = new CitizenAnimationHelper( (AnimatedEntity) Pawn );

        var player = Pawn as MazingPlayer;

        var held = HasTag( "held" );
        var isGhost = !(player?.IsAlive ?? true);

        var lookPos = Pawn.AimRay.Position + EyeRotation.Forward * 200;

        if ( player != null )
        {
            if ( player.HeldEntity != null )
            {
                player.SetAnimParameter( "b_vr", true );
                player.SetAnimParameter( "aim_body_weight", 0.75f );
                player.SetAnimParameter( "left_hand_ik.position", new Vector3( 6f, 14f, 64f ) );
                player.SetAnimParameter( "right_hand_ik.position", new Vector3( 6f, -14f, 64f ) );
                player.SetAnimParameter( "left_hand_ik.rotation", Rotation.From( -65f, 87f, 7f ) );
                player.SetAnimParameter( "right_hand_ik.rotation", Rotation.From( -115f, 87f, 7f ) );
            }
            else
            {
                player.SetAnimParameter( "holdtype", 0 );
                player.SetAnimParameter( "b_vr", false );
                player.SetAnimParameter( "aim_body_weight", 0.5f );
            }
        }
        else
        {
            if ( Pawn is Enemy enemy )
            {
                lookPos = enemy.LookPos;
            }
        }

        animHelper.WithWishVelocity( WishVelocity );
        animHelper.WithVelocity( Velocity );
        animHelper.WithLookAt( lookPos, 1.0f, 1.0f, 0.5f );
        animHelper.AimAngle = rotation;
        animHelper.FootShuffle = shuffle;
        animHelper.DuckLevel = MathX.Lerp( animHelper.DuckLevel, HasTag( "ducked" ) ? 1 : 0, Time.Delta * 10.0f );
        animHelper.VoiceLevel = (Sandbox.Game.IsClient && Client.IsValid()) ? Client.Voice.LastHeard < 0.5f ? Client.Voice.CurrentLevel : 0.0f : 0.0f;
        animHelper.IsGrounded = !isGhost && (GroundEntity != null || held);
        animHelper.IsSitting = false;
        animHelper.IsNoclipping = isGhost && Velocity.WithZ( 0f ).LengthSquared < 50f * 50f;
        animHelper.IsClimbing = false;
        animHelper.IsSwimming = Pawn.GetWaterLevel() >= 0.5f;
        animHelper.IsWeaponLowered = false;

        if ( HasEvent( "jump" ) ) animHelper.TriggerJump();
    }

    public override void Simulate()
    {
        SimulatePhysics();
        SimulateAnimation();
    }

    private void WallGapAssist()
    {
        // Only if moving in a cardinal direction
        if ( MathF.Abs( WishVelocity.x ) >= 0.25f == MathF.Abs( WishVelocity.y ) >= 0.25f ) return;

        var game = MazingGame.Current;

        var dir = MazeData.GetDirection( WishVelocity );
        var cell = Pawn.GetCellIndex();

        // Only if not moving directly into a wall
        if ( game.CurrentMaze.GetWall( cell, dir ) ) return;

        var normal = ((GridCoord) dir).Normal;
        var tangent = new Vector3( -normal.y, normal.x );

        var frac = Vector3.Dot( tangent, Pawn.Position ) / 48f - 0.5f;

        frac -= MathF.Floor( frac ) + 0.5f;

        // Only if not already in the middle of a tile
        if ( MathF.Abs( frac ) < 1f / 32f ) return;

        var sideDir = MazeData.GetDirection( tangent * frac );

        if ( game.CurrentMaze.GetWall( cell + sideDir, dir ) || game.CurrentMaze.GetWall( cell + dir, sideDir ) )
        {
            WishVelocity -= tangent * frac;
        }
    }

    public virtual float GetWishSpeed()
    {
        if ( Pawn is MazingPlayer { HeldEntity.IsHeavy: true } )
        {
            return DefaultSpeed * 2f / 3f;
        }

        return DefaultSpeed;
    }

    public void VaultMove()
    {
        var sinceVault = SinceVault;

        var groundPos = Vector3.Lerp( VaultOrigin, VaultTarget, sinceVault / VaultTime );
        var height = Math.Clamp( 1f - MathF.Pow( 2f * sinceVault / VaultTime - 1f, 2f ), 0f, 1f );

        Position = Vector3.Up * height * VaultHeight + groundPos;

        if ( Game.IsServer && sinceVault > VaultTime * 0.75f && Game.IsServer && Pawn is MazingPlayer player )
        {
            var result = TraceBBox( Position, Position - Vector3.Up * 16f );

            if ( result.Hit && result.Entity is MazingPlayer otherPlayer
                            && !otherPlayer.IsVaulting && result.Entity.Position.z < Position.z - 32f
                            && (otherPlayer.Position - Position).WithZ( 0f ).LengthSquared <= 24f * 24f )
            {
                otherPlayer.PickUp( player );
            }
        }
    }

    public virtual void WalkMove()
    {
        var wishdir = WishVelocity.Normal;
        var wishspeed = WishVelocity.Length;

        WishVelocity = WishVelocity.WithZ( 0 );
        WishVelocity = WishVelocity.Normal * wishspeed;

        Velocity = Velocity.WithZ( 0 );
        Accelerate( wishdir, wishspeed, 0, Acceleration );
        Velocity = Velocity.WithZ( 0 );

        //   Player.SetAnimParam( "forward", Input.Forward );
        //   Player.SetAnimParam( "sideward", Input.Right );
        //   Player.SetAnimParam( "wishspeed", wishspeed );
        //   Player.SetAnimParam( "walkspeed_scale", 2.0f / 190.0f );
        //   Player.SetAnimParam( "runspeed_scale", 2.0f / 320.0f );

        //  DebugOverlay.Text( 0, Pos + Vector3.Up * 100, $"forward: {Input.Forward}\nsideward: {Input.Right}" );

        // Add in any base velocity to the current velocity.
        Velocity += BaseVelocity;

        try
        {
            if ( Velocity.Length < 1.0f )
            {
                Velocity = Vector3.Zero;
                return;
            }

            // first try just moving to the destination
            var dest = (Position + Velocity * Time.Delta).WithZ( Position.z );

            var pm = TraceBBox( Position, dest );

            if ( pm.Fraction == 1 )
            {
                Position = pm.EndPosition;
                StayOnGround();
                return;
            }

            StepMove();
        }
        finally
        {
            // Now pull the base velocity back out.   Base velocity is set if you are on a moving object, like a conveyor (or maybe another monster?)
            Velocity -= BaseVelocity;
        }

        StayOnGround();
    }

    protected virtual MoveHelper CreateMoveHelper()
    {
        var trace = Trace.Capsule( Capsule.FromHeightAndRadius( BodyHeight, BodyGirth * 0.5f ), 0, 0 )
            .WorldAndEntities()
            .Ignore( Pawn );

        ConfigureTraceTags( ref trace );

        return new MoveHelper( Position, Velocity )
        {
            Trace = trace,
            MaxStandableAngle = GroundAngle
        };
    }

    public virtual void StepMove()
    {
        var mover = CreateMoveHelper();
        mover.TryMoveWithStep( Time.Delta, StepSize );

        Position = mover.Position;
        Velocity = mover.Velocity;
    }

    public virtual void Move()
    {
        var mover = CreateMoveHelper();
        mover.MaxStandableAngle = GroundAngle;

        mover.TryMove( Time.Delta );

        Position = mover.Position;
        Velocity = mover.Velocity;
    }

    /// <summary>
    /// Add our wish direction and speed onto our velocity
    /// </summary>
    public virtual void Accelerate( Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
    {
        if ( speedLimit > 0 && wishspeed > speedLimit )
            wishspeed = speedLimit;

        // See if we are changing direction a bit
        var currentspeed = Velocity.Dot( wishdir );

        // Reduce wishspeed by the amount of veer.
        var addspeed = wishspeed - currentspeed;

        // If not going to add any speed, done.
        if ( addspeed <= 0 )
            return;

        // Determine amount of acceleration.
        var accelspeed = acceleration * Time.Delta * wishspeed * (IsGhost ? 1f : SurfaceFriction);

        // Cap at addspeed
        if ( accelspeed > addspeed )
            accelspeed = addspeed;

        Velocity += wishdir * accelspeed;
    }

    /// <summary>
    /// Remove ground friction from velocity
    /// </summary>
    public virtual void ApplyFriction( float frictionAmount = 1.0f )
    {
        // Calculate speed
        var speed = Velocity.Length;
        if ( speed < 0.1f ) return;

        // Bleed off some speed, but if we have less than the bleed
        //  threshold, bleed the threshold amount.
        float control = (speed < StopSpeed) ? StopSpeed : speed;

        // Add the amount to the drop amount.
        var drop = control * Time.Delta * frictionAmount;

        // scale the velocity
        float newspeed = speed - drop;
        if ( newspeed < 0 ) newspeed = 0;

        if ( newspeed != speed )
        {
            newspeed /= speed;
            Velocity *= newspeed;
        }
    }

    public virtual void Vault( GridCoord target, bool withCooldown = true )
    {
        var game = MazingGame.Current;

        if ( Debug )
        {
            DebugOverlay.Box( game.CellToPosition( target.Row + 0.25f, target.Col + 0.25f ),
                game.CellToPosition( target.Row + 0.75f, target.Col + 0.75f ),
                Color.White, 1f );
        }

        SinceVault = 0f;

        if ( withCooldown )
        {
            _localSinceVault = 0;
            NextVault = -VaultCooldown;
        }

        VaultOrigin = Position;
        VaultTarget = game.CellToPosition( target.Row + 0.5f, target.Col + 0.5f );

        Velocity = Vector2.Zero;

        ClearGroundEntity();

        float flGroundFactor = 1.0f;
        float flMul = 268.3281572999747f * 1.2f;
        float startz = Velocity.z;

        Velocity = Velocity.WithZ( startz + flMul * flGroundFactor );
        Velocity -= new Vector3( 0, 0, Gravity * 0.5f ) * Time.Delta;

        if ( Game.IsServer )
        {
            (Pawn as MazingPlayer)?.OnVault();
        }

        AddEvent( "vault" );
    }

    public virtual void AirMove()
    {
        var wishdir = WishVelocity.Normal;
        var wishspeed = WishVelocity.Length;

        Accelerate( wishdir, wishspeed, AirControl, AirAcceleration );

        Velocity += BaseVelocity;

        Move();

        Velocity -= BaseVelocity;
    }

    public virtual void CategorizePosition( bool bStayOnGround )
    {
        SurfaceFriction = 1.0f;

        // Doing this before we move may introduce a potential latency in water detection, but
        // doing it after can get us stuck on the bottom in water if the amount we move up
        // is less than the 1 pixel 'threshold' we're about to snap to.	Also, we'll call
        // this several times per frame, so we really need to avoid sticking to the bottom of
        // water on each call, and the converse case will correct itself if called twice.
        //CheckWater();

        var point = Position - Vector3.Up * 2;
        var vBumpOrigin = Position;

        bool bMovingUpRapidly = Velocity.z > MaxNonJumpVelocity;
        bool bMoveToEndPos = false;

        if ( GroundEntity != null ) // and not underwater
        {
            bMoveToEndPos = true;
            point.z -= StepSize;
        }
        else if ( bStayOnGround )
        {
            bMoveToEndPos = true;
            point.z -= StepSize;
        }

        if ( bMovingUpRapidly ) // or ladder and moving up
        {
            ClearGroundEntity();
            return;
        }

        var pm = TraceBBox( vBumpOrigin, point, 4.0f );

        if ( pm.Entity == null || Vector3.GetAngle( Vector3.Up, pm.Normal ) > GroundAngle )
        {
            ClearGroundEntity();
            bMoveToEndPos = false;

            if ( Velocity.z > 0 )
                SurfaceFriction = 0.25f;
        }
        else
        {
            UpdateGroundEntity( pm );
        }

        if ( bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
        {
            Position = pm.EndPosition;
        }

        if ( Position.z < -512f )
        {
            Position = Position.WithZ( -512f );
            Velocity = Velocity.WithZ( 0f );
        }
    }

    /// <summary>
    /// We have a new ground entity
    /// </summary>
    public virtual void UpdateGroundEntity( TraceResult tr )
    {
        GroundNormal = tr.Normal;

        // VALVE HACKHACK: Scale this to fudge the relationship between vphysics friction values and player friction values.
        // A value of 0.8f feels pretty normal for vphysics, whereas 1.0f is normal for players.
        // This scaling trivially makes them equivalent.  REVISIT if this affects low friction surfaces too much.
        SurfaceFriction = tr.Surface.Friction * 1.25f;
        if ( SurfaceFriction > 1 ) SurfaceFriction = 1;

        if ( GroundEntity == null && Pawn is not Enemy )
        {
            Sound.FromWorld( "player.land", Position );

            if ( Pawn is MazingPlayer player && player.IsSpawning )
            {
                player.IsSpawning = false;
            }
        }

        GroundEntity = tr.Entity;

        if ( GroundEntity != null )
        {
            BaseVelocity = GroundEntity.Velocity;
        }
    }

    /// <summary>
    /// We're no longer on the ground, remove it
    /// </summary>
    public virtual void ClearGroundEntity()
    {
        if ( GroundEntity == null ) return;

        GroundEntity = null;
        GroundNormal = Vector3.Up;
        SurfaceFriction = 1.0f;
    }

    /// <summary>
    /// Traces the current bbox and returns the result.
    /// liftFeet will move the start position up by this amount, while keeping the top of the bbox at the same
    /// position. This is good when tracing down because you won't be tracing through the ceiling above.
    /// </summary>
    public override TraceResult TraceBBox( Vector3 start, Vector3 end, float liftFeet = 0.0f )
    {
        return TraceBBox( start, end, mins, maxs, liftFeet );
    }

    private void ConfigureTraceTags( ref Trace trace )
    {
        if ( !IsVaulting )
        {
            trace = trace.WithAnyTags( "solid" );
        }

        if ( Pawn is MazingPlayer player )
        {
            if ( player.IsAliveInMaze )
            {
                trace = trace.WithAnyTags( "player", "trap" );
            }
            else if ( !player.IsAlive )
            {
                trace = trace.WithAnyTags( "exit" );
            }
        }
        else if ( Pawn is Enemy )
        {
            trace = trace.WithAnyTags( "enemy" );
            trace = trace.WithAnyTags( "exit" );
        }
    }

    public override TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
    {
        if ( liftFeet > 0 )
        {
            start += Vector3.Up * liftFeet;
            maxs = maxs.WithZ( maxs.z - liftFeet );
        }

        var trace = Trace.Capsule( Capsule.FromHeightAndRadius( BodyHeight, BodyGirth * 0.5f ), start, end )
            .WithAnyTags( "playerclip", "passbullets" )
            .Ignore( Pawn );

        ConfigureTraceTags( ref trace );

        var tr = trace.Run();

        return tr;
    }

    /// <summary>
    /// Try to keep a walking player on the ground when running down slopes etc
    /// </summary>
    public virtual void StayOnGround()
    {
        var start = Position + Vector3.Up * 2;
        var end = Position + Vector3.Down * StepSize;

        // See how far up we can go without getting stuck
        var trace = TraceBBox( Position, start );
        start = trace.EndPosition;

        // Now trace down from a known safe position
        trace = TraceBBox( start, end );

        if ( trace.Fraction <= 0 ) return;
        if ( trace.Fraction >= 1 ) return;
        if ( trace.StartedSolid ) return;
        if ( Vector3.GetAngle( Vector3.Up, trace.Normal ) > GroundAngle ) return;

        // This is incredibly hacky. The real problem is that trace returning that strange value we can't network over.
        // float flDelta = fabs( mv->GetAbsOrigin().z - trace.m_vEndPos.z );
        // if ( flDelta > 0.5f * DIST_EPSILON )

        Position = trace.EndPosition;
    }

    void RestoreGroundPos()
    {
        if ( GroundEntity == null || GroundEntity.IsWorld )
            return;

        //var Position = GroundEntity.Transform.ToWorld( GroundTransform );
        //Pos = Position.Position;
    }

    void SaveGroundPos()
    {
        if ( GroundEntity == null || GroundEntity.IsWorld )
            return;

        //GroundTransform = GroundEntity.Transform.ToLocal( new Transform( Pos, Rot ) );
    }
}
