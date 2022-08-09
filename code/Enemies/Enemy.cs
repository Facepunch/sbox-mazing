using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class EnemySpawnAttribute : Attribute
{
    /// <summary>
    /// Level index when this enemy will first spawn (0 is the starting level).
    /// </summary>
    public int FirstLevel { get; set; }

    /// <summary>
    /// Last level index when this enemy is allowed to spawn (0 is the starting level).
    /// </summary>
    public int LastLevel { get; set; } = int.MaxValue;

    /// <summary>
    /// How many levels between new instances of this enemy spawning (1 is every level, 0 is only on <see cref="FirstLevel"/>).
    /// </summary>
    public int SpawnPeriod { get; set; } = 1;

    public bool ShouldSpawn( int levelIndex )
    {
        if (levelIndex < FirstLevel) return false;
        if ( levelIndex > LastLevel ) return false;
        if (levelIndex > FirstLevel && SpawnPeriod <= 0) return false;
        if ((levelIndex - FirstLevel) % SpawnPeriod != 0) return false;

        return true;
    }
}

abstract partial class Enemy : AnimatedEntity
{
    public virtual float MoveSpeed => 100f;

    public PawnController Controller { get; set; }
    public PawnAnimator Animator { get; set; }

    public (int Row, int Col) TargetCell { get; set; }

    public (float Row, float Col) CurrentCell => Game.PositionToCell( Position );
    public (int Row, int Col) CurrentCellIndex => Game.PositionToCellIndex( Position );
    public Direction FacingDirection => MazeData.GetDirection( EyeRotation.Forward );

    public MazingGame Game => MazingGame.Current;
    
    private TimeSince[,] _cellVisitTimes;
    
    private MazeData _lastMaze;

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/citizen/citizen.vmdl");

        Controller = new MazingWalkController
        {
            DefaultSpeed = MoveSpeed
        };

        Animator = new MazingPlayerAnimator();

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        TargetCell = CurrentCellIndex;
    }
    
    [Event.Tick.Server]
    private void ServerTick()
    {
        if (_lastMaze != Game.CurrentMaze)
        {
            _lastMaze = Game.CurrentMaze;
            TargetCell = CurrentCellIndex;

            _cellVisitTimes = new TimeSince[Game.CurrentMaze.Rows, Game.CurrentMaze.Cols];
        }

        var cell = CurrentCellIndex;

        if ( !IsInBounds( cell.Row, cell.Col ) )
        {
            return;
        }
        
        _cellVisitTimes[cell.Row, cell.Col] = 0f;

        var targetPos = Game.CellToPosition( TargetCell.Row + 0.5f, TargetCell.Col + 0.5f );

        if ( (Position - targetPos).WithZ( 0f ).LengthSquared <= 4f * 4f )
        {
            OnReachTarget();
        }
        
        if ( Controller is MazingWalkController walkController )
        {
            walkController.DefaultSpeed = MoveSpeed;

            var dir = Game.CellToPosition(TargetCell.Row + 0.5f, TargetCell.Col + 0.5f) - Position;

            walkController.EnemyWishVelocity = dir;
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

    public bool IsInBounds( int row, int col )
    {
        var maze = Game.CurrentMaze;

        return row >= 0 && row < maze.Rows && col >= 0 && col < maze.Cols;
    }

    public TimeSince GetSinceLastVisited( int row, int col )
    {
        return IsInBounds( row, col ) ? _cellVisitTimes[row, col] : default;
    }

    protected virtual void OnReachTarget()
    {

    }
}
