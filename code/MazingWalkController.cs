using System;
using System.Linq;
using Mazing.Enemies;
using Sandbox;

namespace Mazing;

public partial class MazingWalkController : BasePlayerController
{
    [Net] public float DefaultSpeed { get; set; } = 160f;
    [Net] public float Acceleration { get; set; } = 10.0f;
    [Net] public float AirAcceleration { get; set; } = 50.0f;
    [Net] public float GroundFriction { get; set; } = 8.0f;
    [Net] public float StopSpeed { get; set; } = 100.0f;
    [Net] public float GroundAngle { get; set; } = 46.0f;
    [Net] public float StepSize { get; set; } = 18.0f;
    [Net] public float MaxNonJumpVelocity { get; set; } = 140.0f;
    [Net] public float BodyGirth { get; set; } = 32.0f;
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

    public bool IsVaulting => SinceVault <= VaultTime;

    public bool IsPlayer => Pawn is Player;
    
    public Vector3 EnemyWishVelocity { get; set; }

    [Net, Predicted]
    public Vector3 VaultOrigin { get; set; }

    [Net, Predicted]
    public Vector3 VaultTarget { get; set; }

    [Net]
    public TimeSince SinceVault { get; set; }

    private bool _wasVaulting;
    private bool _wasVaultCooldown;

    public Unstuck Unstuck;
    
    public MazingWalkController()
    {
        Unstuck = new Unstuck(this);
        SinceVault = float.MaxValue;
    }

    /// <summary>
    /// This is temporary, get the hull size for the player's collision
    /// </summary>
    public override BBox GetHull()
    {
        var girth = BodyGirth * 0.5f;
        var mins = new Vector3(-girth, -girth, 0);
        var maxs = new Vector3(+girth, +girth, BodyHeight);

        return new BBox(mins, maxs);
    }
    
    // Duck body height 32
    // Eye Height 64
    // Duck Eye Height 28

    protected Vector3 mins;
    protected Vector3 maxs;

    public virtual void SetBBox( Vector3 mins, Vector3 maxs )
    {
        if (this.mins == mins && this.maxs == maxs)
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

        var mins = new Vector3(-girth, -girth, 0) * Pawn.Scale;
        var maxs = new Vector3(+girth, +girth, BodyHeight) * Pawn.Scale;
        
        SetBBox(mins, maxs);
    }

    protected float SurfaceFriction;

    private void UpdateEyeRotation()
    {
        if ( IsVaulting )
        {
            return;
        }

        if (WishVelocity.LengthSquared <= 0.125f )
        {
            return;
        }

        var dir = WishVelocity;

        if ( Math.Abs(dir.x ) > Math.Abs(dir.y ) )
        {
            dir.y = 0f;
        }
        else
        {
            dir.x = 0f;
        }

        EyeRotation = Rotation.LookAt(dir, Vector3.Up );
    }


    public override void FrameSimulate()
    {
        base.FrameSimulate();

        UpdateEyeRotation();
    }

    private void WallCollision()
    {
        var game = MazingGame.Current;
        var (rowF, colF) = game.PositionToCell(Position);
        var cell = new GridCoord(rowF.FloorToInt(), colF.FloorToInt());

        var wishVelocityAdd = Vector3.Zero;
        var velocityAdd = Vector3.Zero;
        var positionAdd = Vector3.Zero;

        var rowFrac = rowF - cell.Row;
        var colFrac = colF - cell.Col;

        foreach (var (dir, delta) in MazeData.Directions)
        {
            var directWall = game.CurrentMaze.GetWall(cell, dir);

            bool endWall = false;

            if (delta.Row != 0 && Math.Abs(colFrac - 0.5f) > 0.2f)
            {
                if (colFrac > 0.5f)
                {
                    endWall = game.CurrentMaze.GetWall(cell + delta, Direction.East) || game.CurrentMaze.GetWall(cell + (0, 1), dir);
                }
                else
                {
                    endWall = game.CurrentMaze.GetWall(cell + delta, Direction.West) || game.CurrentMaze.GetWall(cell - (0, 1), dir);
                }
            }
            else if (delta.Col != 0 && Math.Abs(rowFrac - 0.5f) > 0.2f)
            {
                if (rowFrac > 0.5f)
                {
                    endWall = game.CurrentMaze.GetWall(cell + delta, Direction.South) || game.CurrentMaze.GetWall(cell + (1, 0), dir);
                }
                else
                {
                    endWall = game.CurrentMaze.GetWall(cell + delta, Direction.North) || game.CurrentMaze.GetWall(cell - (1, 0), dir);
                }
            }

            if (!directWall && !endWall)
            {
                continue;
            }

            var mid = (game.CellToPosition(cell.Row + 0.5f, cell.Col + 0.5f) + game.CellToPosition(cell.Row + delta.Row + 0.5f, cell.Col + delta.Col + 0.5f)) * 0.5f;
            var diff = game.CellToPosition(cell + delta) - game.CellToPosition(cell);
            var normal = diff.Normal;
            var size = new Vector3(Math.Abs(diff.y) + 6f, Math.Abs(diff.x) + 6f, 0f);

            var canVault = directWall && (delta.Row == 0 || cell.Row + delta.Row >= 0 && cell.Row + delta.Row < game.CurrentMaze.Rows)
                           && (delta.Col == 0 || cell.Col + delta.Col >= 0 && cell.Col + delta.Col < game.CurrentMaze.Cols);

            var playerDist = Vector3.Dot(mid - Position, normal) - 24f;

            var moveDot = Vector3.Dot(WishVelocity, normal);

            //
            // If you're too close to a wall, immediately move back and cancel any velocity
            // in that direction
            //
            if (playerDist < 0f)
            {
                if (Velocity.Length > 15f) // hack so slow movement doesn't jitter
                    positionAdd += playerDist * normal;

                var velDot = Vector3.Dot(Velocity, normal);

                if (directWall && velDot > 0f)
                {
                    velocityAdd -= velDot * normal;
                }
            }

            var perpMoveDot = Math.Abs(Vector3.Dot(WishVelocity, new Vector3(normal.y, -normal.x)));

            //
            // If you're trying to walk into a wall, cancel that movement if either:
            //   1) you are also moving in a perpendicular direction
            //   2) you aren't currently facing that direction
            //
            if (moveDot > 0f && (perpMoveDot > 0.5f || Vector3.Dot(normal, EyeRotation.Forward) > 0.5f))
            {
                wishVelocityAdd -= moveDot * normal * Math.Clamp(1f - playerDist, 0f, 1f);
            }

            //
            // If you're walking towards a gap between walls, but you're not cleanly going through
            // the middle, walk perpendicular so you'll be in the middle of the gap
            //
            if (!directWall && moveDot > 0f)
            {
                var perp = delta.Row != 0 ? colFrac > 0.5f
                        ? new Vector3(-1f, 0f)
                        : new Vector3(1f, 0f)
                    : rowFrac > 0.5f ? new Vector3(0f, -1f) : new Vector3(0f, 1f);

                wishVelocityAdd += perp;
            }

            if (!IsVaulting && !IsGhost && SinceVault > VaultCooldown && canVault && Vector3.Dot(EyeRotation.Forward, normal) > 0.6f && IsPlayer && Input.Down(InputButton.Jump))
            {
                Vault(cell + delta);
            }

            if (Debug)
            {
                DebugOverlay.Box(mid - size * 0.5f, mid + size * 0.5f, canVault ? Color.Blue : Color.Red,
                    depthTest: false);
            }
        }

        Position += positionAdd;
        WishVelocity += wishVelocityAdd;
        Velocity += velocityAdd;
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

    public override void Simulate()
    {
        EyeLocalPosition = Vector3.Up * (EyeHeight * Pawn.Scale);
        UpdateBBox();

        EyeLocalPosition += TraceOffset;

        RestoreGroundPos();

        if (Unstuck.TestAndFix())
            return;

        //
        // Start Gravity
        //
        Velocity -= new Vector3(0, 0, Gravity * 0.5f) * Time.Delta;
        Velocity += new Vector3(0, 0, BaseVelocity.z) * Time.Delta;
        
        WishVelocity = default;

        BaseVelocity = BaseVelocity.WithZ(0);

        if ( !IsVaulting && _wasVaulting )
        {
            AddEvent( "vault_end" );
        }

        _wasVaulting = IsVaulting;
        
        if ( IsVaulting )
        {
            VaultMove();
        }
        else
        {
            var game = MazingGame.Current;
            var (rowF, colF) = game.PositionToCell( Position );
            var cell = new GridCoord(rowF.FloorToInt(), colF.FloorToInt());

            var vaultOnCooldown = SinceVault < VaultCooldown;

            if ( !vaultOnCooldown && _wasVaultCooldown )
            {
                Sound.FromEntity( "player.recharge", Pawn );
                AddEvent( "vault_reset" );
            }

            _wasVaultCooldown = vaultOnCooldown;

            if ( Debug )
            {
                if ( SinceVault < VaultCooldown )
                {
                    DebugOverlay.Text( $"Vault: {VaultCooldown - SinceVault}", Position + Vector3.Up * 128f, 0,
                        Color.Green, 0f,
                        maxDistance: float.MaxValue );
                }

                DebugOverlay.Box( game.CellToPosition(cell), game.CellToPosition( cell.Row + 1f, cell.Col + 1f ),
                    new Color( 0.5f, 0.5f, 0.5f, 1f ), depthTest: false );
            }

            if ( Pawn is MazingPlayer player )
            {
                if ( player.HasExited )
                {
                    WishVelocity = game.CellCenterToPosition( game.ExitCell ) - player.Position;
                }
                else
                {
                    WishVelocity = new Vector3(-Input.Left, Input.Forward, 0);
                }
            }
            else
            {
                WishVelocity = EnemyWishVelocity;
            }

            if ( !IsGhost )
            {
                WallCollision();
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

            //
            // Work out wish velocity.. just take input, rotate it to view, clamp to -1, 1
            //
            var inSpeed = WishVelocity.Length.Clamp( 0, 1 );

            WishVelocity = WishVelocity.WithZ( 0 );
            UpdateEyeRotation();

            WishVelocity = WishVelocity.Normal * inSpeed;
            WishVelocity *= GetWishSpeed();

            if (IsGhost)
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
        Velocity -= new Vector3(0, 0, Gravity * 0.5f) * Time.Delta;
        
        if ( GroundEntity != null && !IsGhost )
        {
            Velocity = Velocity.WithZ(0);
        }

        SaveGroundPos();

        if (Debug)
        {
            DebugOverlay.Box(Position + TraceOffset, mins, maxs, Color.Red);
            DebugOverlay.Box(Position, mins, maxs, Color.Blue);

            var lineOffset = 0;
            if (Host.IsServer) lineOffset = 10;

            DebugOverlay.ScreenText($"        Position: {Position}", lineOffset + 0);
            DebugOverlay.ScreenText($"        Velocity: {Velocity}", lineOffset + 1);
            DebugOverlay.ScreenText($"    BaseVelocity: {BaseVelocity}", lineOffset + 2);
            DebugOverlay.ScreenText($"    GroundEntity: {GroundEntity} [{GroundEntity?.Velocity}]", lineOffset + 3);
            DebugOverlay.ScreenText($" SurfaceFriction: {SurfaceFriction}", lineOffset + 4);
            DebugOverlay.ScreenText($"    WishVelocity: {WishVelocity}", lineOffset + 5);
        }

    }

    public virtual float GetWishSpeed()
    {
        return DefaultSpeed;
    }

    public void VaultMove()
    {
        var groundPos = Vector3.Lerp( VaultOrigin, VaultTarget, SinceVault / VaultTime );
        var height = Math.Clamp( 1f - MathF.Pow( 2f * SinceVault / VaultTime - 1f, 2f ), 0f, 1f );

        Position = Vector3.Up * height * VaultHeight + groundPos + Vector3.Up;
    }

    public virtual void WalkMove()
    {
        var wishdir = WishVelocity.Normal;
        var wishspeed = WishVelocity.Length;

        WishVelocity = WishVelocity.WithZ(0);
        WishVelocity = WishVelocity.Normal * wishspeed;

        Velocity = Velocity.WithZ(0);
        Accelerate(wishdir, wishspeed, 0, Acceleration);
        Velocity = Velocity.WithZ(0);

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
            if (Velocity.Length < 1.0f)
            {
                Velocity = Vector3.Zero;
                return;
            }

            // first try just moving to the destination
            var dest = (Position + Velocity * Time.Delta).WithZ(Position.z);

            var pm = TraceBBox(Position, dest);

            if (pm.Fraction == 1)
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

    public virtual void StepMove()
    {
        MoveHelper mover = new MoveHelper(Position, Velocity);
        mover.Trace = mover.Trace.Size(mins, maxs).Ignore(Pawn);
        mover.MaxStandableAngle = GroundAngle;

        mover.TryMoveWithStep(Time.Delta, StepSize);

        Position = mover.Position;
        Velocity = mover.Velocity;
    }

    public virtual void Move()
    {
        MoveHelper mover = new MoveHelper(Position, Velocity);
        
        mover.Trace = mover.Trace.Size(mins, maxs).Ignore(Pawn);
        mover.MaxStandableAngle = GroundAngle;

        mover.TryMove(Time.Delta);

        Position = mover.Position;
        Velocity = mover.Velocity;
    }

    /// <summary>
    /// Add our wish direction and speed onto our velocity
    /// </summary>
    public virtual void Accelerate( Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
    {
        if (speedLimit > 0 && wishspeed > speedLimit)
            wishspeed = speedLimit;

        // See if we are changing direction a bit
        var currentspeed = Velocity.Dot(wishdir);

        // Reduce wishspeed by the amount of veer.
        var addspeed = wishspeed - currentspeed;

        // If not going to add any speed, done.
        if (addspeed <= 0)
            return;

        // Determine amount of acceleration.
        var accelspeed = acceleration * Time.Delta * wishspeed * (IsGhost ? 1f : SurfaceFriction);

        // Cap at addspeed
        if (accelspeed > addspeed)
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
        if (speed < 0.1f) return;

        // Bleed off some speed, but if we have less than the bleed
        //  threshold, bleed the threshold amount.
        float control = (speed < StopSpeed) ? StopSpeed : speed;

        // Add the amount to the drop amount.
        var drop = control * Time.Delta * frictionAmount;

        // scale the velocity
        float newspeed = speed - drop;
        if (newspeed < 0) newspeed = 0;

        if (newspeed != speed)
        {
            newspeed /= speed;
            Velocity *= newspeed;
        }
    }

    public virtual void Vault( GridCoord target )
    {
        if (GroundEntity == null)
            return;

        var game = MazingGame.Current;

        if ( Debug )
        {
            DebugOverlay.Box(game.CellToPosition(target.Row + 0.25f, target.Col + 0.25f),
                game.CellToPosition(target.Row + 0.75f, target.Col + 0.75f),
                Color.White, 1f);
        }
        
        SinceVault = 0f;
        _wasVaultCooldown = true;

        VaultOrigin = Position;
        VaultTarget = game.CellToPosition( target.Row + 0.5f, target.Col + 0.5f );

        Velocity = Vector2.Zero;

        ClearGroundEntity();

        float flGroundFactor = 1.0f;
        float flMul = 268.3281572999747f * 1.2f;
        float startz = Velocity.z;
        
        Velocity = Velocity.WithZ(startz + flMul * flGroundFactor);
        Velocity -= new Vector3(0, 0, Gravity * 0.5f) * Time.Delta;

        if ( Host.IsServer )
        {
            (Pawn as MazingPlayer)?.OnVault();
        }

        Sound.FromEntity( "player.vault", Pawn );

        AddEvent("vault");
    }

    public virtual void AirMove()
    {
        var wishdir = WishVelocity.Normal;
        var wishspeed = WishVelocity.Length;

        Accelerate(wishdir, wishspeed, AirControl, AirAcceleration);

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

        if (GroundEntity != null) // and not underwater
        {
            bMoveToEndPos = true;
            point.z -= StepSize;
        }
        else if (bStayOnGround)
        {
            bMoveToEndPos = true;
            point.z -= StepSize;
        }

        if (bMovingUpRapidly) // or ladder and moving up
        {
            ClearGroundEntity();
            return;
        }

        var pm = TraceBBox(vBumpOrigin, point, 4.0f);

        if (pm.Entity == null || Vector3.GetAngle(Vector3.Up, pm.Normal) > GroundAngle)
        {
            ClearGroundEntity();
            bMoveToEndPos = false;

            if (Velocity.z > 0)
                SurfaceFriction = 0.25f;
        }
        else
        {
            UpdateGroundEntity(pm);
        }

        if (bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f)
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
        if (SurfaceFriction > 1) SurfaceFriction = 1;

        if ( GroundEntity == null && Pawn is not Enemy )
        {
            Sound.FromWorld("player.land", Position);
        }

        GroundEntity = tr.Entity;

        if (GroundEntity != null)
        {
            BaseVelocity = GroundEntity.Velocity;
        }
    }

    /// <summary>
    /// We're no longer on the ground, remove it
    /// </summary>
    public virtual void ClearGroundEntity()
    {
        if (GroundEntity == null) return;

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
        return TraceBBox(start, end, mins, maxs, liftFeet);
    }

    public override TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
    {
        if (liftFeet > 0)
        {
            start += Vector3.Up * liftFeet;
            maxs = maxs.WithZ(maxs.z - liftFeet);
        }

        var trace = Trace.Ray( start + TraceOffset, end + TraceOffset )
            .Size( mins, maxs )
            .WithAnyTags( "playerclip", "passbullets" )
            .Ignore( Pawn );

        if ( !IsVaulting )
        {
            trace = trace.WithAnyTags( "solid" );
        }

        if ( Pawn is MazingPlayer player )
        {
            if ( player.IsAliveInMaze )
            {
                trace = trace.WithAnyTags( "player" );
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

        var tr = trace.Run();

        tr.EndPosition -= TraceOffset;

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
        var trace = TraceBBox(Position, start);
        start = trace.EndPosition;

        // Now trace down from a known safe position
        trace = TraceBBox(start, end);

        if (trace.Fraction <= 0) return;
        if (trace.Fraction >= 1) return;
        if (trace.StartedSolid) return;
        if (Vector3.GetAngle(Vector3.Up, trace.Normal) > GroundAngle) return;

        // This is incredibly hacky. The real problem is that trace returning that strange value we can't network over.
        // float flDelta = fabs( mv->GetAbsOrigin().z - trace.m_vEndPos.z );
        // if ( flDelta > 0.5f * DIST_EPSILON )

        Position = trace.EndPosition;
    }

    void RestoreGroundPos()
    {
        if (GroundEntity == null || GroundEntity.IsWorld)
            return;

        //var Position = GroundEntity.Transform.ToWorld( GroundTransform );
        //Pos = Position.Position;
    }

    void SaveGroundPos()
    {
        if (GroundEntity == null || GroundEntity.IsWorld)
            return;

        //GroundTransform = GroundEntity.Transform.ToLocal( new Transform( Pos, Rot ) );
    }
}
