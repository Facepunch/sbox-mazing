using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

abstract partial class Enemy : AnimatedEntity
{
    public virtual float MoveSpeed => 100f;

    public ClothingContainer Clothing { get; } = new();

    public PawnController Controller { get; set; }
    public PawnAnimator Animator { get; set; }

    public (int Row, int Col) TargetCell { get; set; }

    public (float Row, float Col) CurrentCell => Game.PositionToCell( Position );
    public (int Row, int Col) CurrentCellIndex => Game.PositionToCellIndex( Position );
    public Direction FacingDirection => MazeData.GetDirection( EyeRotation.Forward );

    public MazingGame Game => MazingGame.Current;

    public void Respawn()
    {
        SetModel("models/citizen/citizen.vmdl");

        Controller = new MazingWalkController
        {
            DefaultSpeed = MoveSpeed
        };

        Animator = new MazingPlayerAnimator();

        Clothing.DressEntity(this);

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        TargetCell = CurrentCellIndex;

        OnRespawn();
    }

    protected virtual void OnRespawn()
    {
    }

    [Event.Tick.Server]
    private void ServerTick()
    {
        var targetPos = Game.CellToPosition( TargetCell.Row + 0.5f, TargetCell.Col + 0.5f );

        if ( (Position - targetPos).WithZ( 0f ).LengthSquared <= 4f * 4f )
        {
            OnReachTarget();
        }
        
        if ( Controller is MazingWalkController walkController )
        {
            walkController.DefaultSpeed = MoveSpeed;

            var dir = Game.CellToPosition(TargetCell.Row + 0.5f, TargetCell.Col + 0.5f) - Position;

            walkController.EnemyWishVelocity =
                Math.Abs( dir.x ) > Math.Abs( dir.y ) ? dir.WithY( 0f ) : dir.WithX( 0f );
        }

        Controller?.Simulate( default, this, null );
        Animator?.Simulate( default, this, null );
    }

    public bool CanWalkInDirection( Vector3 dir )
    {
        return CanWalkInDirection( MazeData.GetDirection( dir ) );
    }

    public bool CanWalkInDirection( Direction direction )
    {
        var cell = CurrentCellIndex;

        return !Game.CurrentMaze.GetWall(cell.Row, cell.Col, direction );
    }

    protected virtual void OnReachTarget()
    {

    }
}
