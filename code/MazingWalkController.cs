using System;
using System.Linq;
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
    [Net] public float Gravity { get; set; } = 800.0f;
    [Net] public float AirControl { get; set; } = 30.0f;
    [Net] public float VaultTime { get; set; } = 0.6f;
    //[Net] public float PostVaultTime { get; set; } = 0.2f;
    [Net] public float VaultHeight { get; set; } = 192f;
    [Net] public float VaultCooldown { get; set; } = 3.5f;

    public bool IsVaulting => SinceVault < VaultTime;

    public bool IsPlayer => Pawn is Player;
    
    public Vector3 EnemyWishVelocity { get; set; }

    [Net, Predicted]
    public Vector3 VaultOrigin { get; set; }

    [Net, Predicted]
    public Vector3 VaultTarget { get; set; }

    [Net]
    public TimeSince SinceVault { get; set; }

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

        if ( Pawn is MazingPlayer player && player.HasExited )
        {
            AirMove();
            CategorizePosition( false );
        }
        else if ( IsVaulting )
        {
            VaultMove();
        }
        else
        {
            var game = MazingGame.Current;
            var (rowF, colF) = game.PositionToCell( Position );
            var (row, col) = (rowF.FloorToInt(), colF.FloorToInt());

            if ( SinceVault < VaultCooldown )
            {
                DebugOverlay.Text($"Vault: {VaultCooldown - SinceVault}", Position + Vector3.Up * 128f, 0, Color.Green, 0f,
                    maxDistance: float.MaxValue);
            }

            if ( Debug )
            {
                DebugOverlay.Box( game.CellToPosition( row, col ), game.CellToPosition( row + 1f, col + 1f ),
                    new Color( 0.5f, 0.5f, 0.5f, 1f ), depthTest: false );
            }

            if ( IsPlayer )
            {
                WishVelocity = new Vector3(-Input.Left, Input.Forward, 0);
            }
            else
            {
                WishVelocity = EnemyWishVelocity;
            }

            var wishVelocityAdd = Vector3.Zero;
            var velocityAdd = Vector3.Zero;
            var positionAdd = Vector3.Zero;

            var rowFrac = rowF - row;
            var colFrac = colF - col;

            foreach ( var (dir, dRow, dCol) in MazeData.Directions )
            {
                var directWall = game.CurrentMaze.GetWall( row, col, dir );

                bool endWall = false;

                if ( dRow != 0 && Math.Abs(colFrac - 0.5f) > 0.2f )
                {
                    if ( colFrac > 0.5f )
                    {
                        endWall = game.CurrentMaze.GetWall( row + dRow, col + dCol, Direction.East ) || game.CurrentMaze.GetWall( row, col + 1, dir );
                    }
                    else
                    {
                        endWall = game.CurrentMaze.GetWall( row + dRow, col + dCol, Direction.West ) || game.CurrentMaze.GetWall(row, col - 1, dir);
                    }
                }
                else if ( dCol != 0 && Math.Abs( rowFrac - 0.5f ) > 0.2f )
                {
                    if ( rowFrac > 0.5f )
                    {
                        endWall = game.CurrentMaze.GetWall( row + dRow, col + dCol, Direction.South ) || game.CurrentMaze.GetWall(row + 1, col, dir);
                    }
                    else
                    {
                        endWall = game.CurrentMaze.GetWall( row + dRow, col + dCol, Direction.North ) || game.CurrentMaze.GetWall(row - 1, col, dir);
                    }
                }

                if ( !directWall && !endWall )
                {
                    continue;
                }

                var mid = (game.CellToPosition(row + 0.5f, col + 0.5f) +
                           game.CellToPosition(row + dRow + 0.5f, col + dCol + 0.5f)) * 0.5f;
                var diff = game.CellToPosition(row + dRow, col + dCol) - game.CellToPosition(row, col);
                var normal = diff.Normal;
                var size = new Vector3(Math.Abs(diff.y) + 6f, Math.Abs(diff.x) + 6f, 0f);

                var canVault = directWall && (dRow == 0 || row + dRow >= 0 && row + dRow < game.CurrentMaze.Rows)
                               && (dCol == 0 || col + dCol >= 0 && col + dCol < game.CurrentMaze.Cols);

                var playerDist = Vector3.Dot(mid - Position, normal) - 24f;

                var moveDot = Vector3.Dot(WishVelocity, normal);

                //
                // If you're too close to a wall, immediately move back and cancel any velocity
                // in that direction
                //
                if ( playerDist < 0f )
                {
                    positionAdd += playerDist * normal;

                    var velDot = Vector3.Dot(Velocity, normal);

                    if ( directWall && velDot > 0f )
                    {
                        velocityAdd -= velDot * normal;
                    }
                }

                var perpMoveDot = Math.Abs( Vector3.Dot( WishVelocity, new Vector3( normal.y, -normal.x ) ) );

                //
                // If you're trying to walk into a wall, cancel that movement if either:
                //   1) you are also moving in a perpendicular direction
                //   2) you aren't currently facing that direction
                //
                if ( moveDot > 0f && (perpMoveDot > 0.5f || Vector3.Dot( normal, EyeRotation.Forward ) > 0.5f) )
                {
                    wishVelocityAdd -= moveDot * normal * Math.Clamp( 1f - playerDist, 0f, 1f );
                }

                //
                // If you're walking towards a gap between walls, but you're not cleanly going through
                // the middle, walk perpendicular so you'll be in the middle of the gap
                //
                if ( !directWall && moveDot > 0f )
                {
                    var perp = dRow != 0 ? colFrac > 0.5f
                            ? new Vector3( -1f, 0f )
                            : new Vector3( 1f, 0f )
                        : rowFrac > 0.5f ? new Vector3( 0f, -1f ) : new Vector3( 0f, 1f );

                    wishVelocityAdd += perp;
                }

                if (SinceVault > VaultCooldown && canVault && Vector3.Dot(EyeRotation.Forward, normal) > 0.6f && IsPlayer && Input.Down(InputButton.Jump))
                {
                    CheckVaultButton(row + dRow, col + dCol);
                }

                if ( Debug )
                {
                    DebugOverlay.Box( mid - size * 0.5f, mid + size * 0.5f, canVault ? Color.Blue : Color.Red,
                        depthTest: false );
                }
            }

            Position += positionAdd;
            WishVelocity += wishVelocityAdd;
            Velocity += velocityAdd;

            // Fricion is handled before we add in any base velocity. That way, if we are on a conveyor,
            //  we don't slow when standing still, relative to the conveyor.
            bool bStartOnGround = GroundEntity != null;
            //bool bDropSound = false;
            if ( bStartOnGround )
            {
                //if ( Velocity.z < FallSoundZ ) bDropSound = true;

                Velocity = Velocity.WithZ( 0 );
                //player->m_Local.m_flFallVelocity = 0.0f;

                if ( GroundEntity != null )
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

            bool bStayOnGround = false;
            if ( GroundEntity != null )
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
        
        if (GroundEntity != null)
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

        if ( SinceVault > VaultTime )
        {
            Velocity = Vector3.Zero;
            ((ModelEntity)Pawn).EnableAllCollisions = true;

            CategorizePosition( false );
        }
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
        var accelspeed = acceleration * Time.Delta * wishspeed * SurfaceFriction;

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

    public virtual void CheckVaultButton( int targetRow, int targetCol )
    {
        if (GroundEntity == null)
            return;

        var game = MazingGame.Current;

        if ( Debug )
        {
            DebugOverlay.Box(game.CellToPosition(targetRow + 0.25f, targetCol + 0.25f),
                game.CellToPosition(targetRow + 0.75f, targetCol + 0.75f),
                Color.White, 1f);
        }
        
        SinceVault = 0f;

        VaultOrigin = Position;
        VaultTarget = game.CellToPosition( targetRow + 0.5f, targetCol + 0.5f );

        Velocity = Vector2.Zero;

        ClearGroundEntity();

        float flGroundFactor = 1.0f;
        float flMul = 268.3281572999747f * 1.2f;
        float startz = Velocity.z;
        
        Velocity = Velocity.WithZ(startz + flMul * flGroundFactor);
        Velocity -= new Vector3(0, 0, Gravity * 0.5f) * Time.Delta;

        AddEvent("jump");
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
