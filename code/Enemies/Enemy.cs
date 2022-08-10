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
    public float KillRange { get; } = 16f;

    public virtual float MoveSpeed => 100f;

    public PawnController Controller { get; set; }
    public PawnAnimator Animator { get; set; }

    public GridCoord TargetCell { get; set; }

    public MazingGame Game => MazingGame.Current;
    
    private TimeSince[,] _cellVisitTimes;
    
    private MazeData _lastMaze;

    public TimeSince LastAttack { get; private set; }

    public TimeSince AwakeTime { get; set; }

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/citizen/citizen.vmdl");

        Controller = new MazingWalkController
        {
            DefaultSpeed = MoveSpeed
        };

        Tags.Add( "enemy" );

        Animator = new MazingPlayerAnimator();

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        TargetCell = this.GetCellIndex();
    }
    
    [Event.Tick.Server]
    private void ServerTick()
    {
        OnServerTick();
    }

    protected virtual void OnServerTick()
    {
        var cell = this.GetCellIndex();

        if (_lastMaze != Game.CurrentMaze)
        {
            _lastMaze = Game.CurrentMaze;
            TargetCell = cell;

            AwakeTime = -2.5f - Rand.Float(0.5f);

            _cellVisitTimes = new TimeSince[Game.CurrentMaze.Rows, Game.CurrentMaze.Cols];
        }

        if (!IsInBounds(cell))
        {
            return;
        }

        _cellVisitTimes[cell.Row, cell.Col] = 0f;

        if ( AwakeTime > 0f )
        {
            var targetPos = Game.CellToPosition(TargetCell.Row + 0.5f, TargetCell.Col + 0.5f);

            if ((Position - targetPos).WithZ(0f).LengthSquared <= 2f * 2f)
            {
                OnReachTarget();
            }
            else if (Math.Abs(TargetCell.Row - cell.Row) + Math.Abs(TargetCell.Col - cell.Col) > 1)
            {
                OnReachTarget();
            }
            else if (TargetCell != this.GetCellIndex() && !CanWalkInDirection(targetPos - Position))
            {
                OnReachTarget();
            }
        }

        if ( Controller is MazingWalkController walkController )
        {
            walkController.DefaultSpeed = MoveSpeed;

            var dir = Game.CellToPosition(TargetCell.Row + 0.5f, TargetCell.Col + 0.5f) - Position;

            //DebugOverlay.Box(Game.CellToPosition(TargetCell.Row, TargetCell.Col),
            //    Game.CellToPosition(TargetCell.Row + 1f, TargetCell.Col + 1f),
            //    new Color(0f, 1f, 0f, 0.1f), depthTest: false);


            walkController.EnemyWishVelocity = dir;
        }

        Controller?.Simulate(default, this, null);
        Animator?.Simulate(default, this, null);

        if ( LastAttack < 1f )
        {
            return;
        }

        if ( AwakeTime < 0f )
        {
            Animator?.SetAnimParameter( "holdtype", 0 );
            return;
        }

        Animator?.SetAnimParameter( "holdtype", 5 );

        var closestPlayer = Entity.All.OfType<MazingPlayer>()
            .Where(x => x.IsAliveInMaze && (x.Position - Position).LengthSquared < KillRange * KillRange)
            .MinBy(x => (x.Position - Position).LengthSquared);

        if ( closestPlayer != null )
        {
            LastAttack = 0f;

            Animator?.Trigger( "b_attack" );

            closestPlayer.Kill( (closestPlayer.Position - Position).WithZ( 0f ) );
        }
    }

    public bool CanWalkInDirection( Vector3 dir )
    {
        return CanWalkInDirection( MazeData.GetDirection( dir ) );
    }

    public bool CanWalkInDirection( Direction direction )
    {
        return !Game.CurrentMaze.GetWall(this.GetCellIndex(), direction );
    }

    public bool IsInBounds( GridCoord coord )
    {
        var maze = Game.CurrentMaze;

        return coord.Row >= 0 && coord.Row < maze.Rows && coord.Col >= 0 && coord.Col < maze.Cols;
    }

    public TimeSince GetSinceLastVisited( GridCoord coord )
    {
        return IsInBounds(coord) ? _cellVisitTimes[coord.Row, coord.Col] : default;
    }

    protected virtual void OnReachTarget()
    {

    }

    protected GridCoord GetRandomNeighborCell()
    {
        var cell = this.GetCellIndex();
        var dir = MazeData.Directions.Where( x => CanWalkInDirection( x.Direction ) )
            .OrderBy( x => Rand.Float() - GetSinceLastVisited( cell + x.Delta ) )
            .FirstOrDefault();

        return cell + dir.Delta;
    }
    
    private PathFinder _pathFinder;
    private readonly List<GridCoord> _path = new();

    protected GridCoord GetNextInPathTo( GridCoord coord )
    {
        var cell = this.GetCellIndex();

        _pathFinder ??= new PathFinder();
        _path.Clear();

        _pathFinder.FindPath(cell, coord, _path);

        if ( _path.Count < 2 )
        {
            return cell;
        }

        return _path.Skip( 1 ).First();
    }
}
