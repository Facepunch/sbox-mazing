using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

[AttributeUsage(AttributeTargets.Class)]
public sealed class UnlockLevelAttribute : Attribute
{
    public int Level { get; }

    public UnlockLevelAttribute( int level )
    {
        Level = level;
    }
}

public abstract partial class Enemy : AnimatedEntity
{
    public float KillRange { get; } = 16f;

    public virtual float MoveSpeed => 100f;

    public PawnController Controller { get; set; }
    public PawnAnimator Animator { get; set; }

    public GridCoord TargetCell { get; set; }

    public MazingGame Game => MazingGame.Current;
    
    private TimeSince[,] _cellVisitTimes;

    public TimeSince LastAttack { get; private set; }

    public TimeSince AwakeTime { get; set; }

    public bool IsAwake => AwakeTime > 0f;

    public bool IsDeleting { get; set; }

    public ClothingContainer Clothing { get; set; } = new();

    private static readonly (string Verb, float Weight)[] _sDeathVerbs = new (string Verb, float Weight)[]
    {
        ("killed", 10f),
        ("slapped", 1f),
    };

    private static string GetRandomDeathVerb()
    {
        var totalWeight = _sDeathVerbs.Sum( x => x.Weight );
        var randomWeight = Rand.Float( 0f, totalWeight );

        for ( var i = 0; i < _sDeathVerbs.Length; ++i )
        {
            randomWeight -= _sDeathVerbs[i].Weight;

            if ( randomWeight < 0f )
            {
                return _sDeathVerbs[i].Verb;
            }
        }

        return "killed";
    }

    protected virtual string DeathMessage => $"{{0}} was {GetRandomDeathVerb()} by a {GetType().Name.ToTitleCase()}";

    protected virtual int HoldType => 5;
    public virtual Vector3 LookPos => EyePosition + EyeRotation.Forward * 200;

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

    public void Hide()
    {
        EnableDrawing = false;

        foreach (var child in Children.ToArray())
        {
            if (child is ModelEntity e)
            {
                e.EnableDrawing = false;
            }
        }
    }

    public void Show()
    {
        EnableDrawing = true;

        foreach (var child in Children.ToArray())
        {
            if (child is ModelEntity e)
            {
                e.EnableDrawing = true;
            }
        }
    }
    
    [Event.Tick.Server]
    private void ServerTick()
    {
        var cell = this.GetCellIndex();

        if (_cellVisitTimes == null)
        {
            TargetCell = cell;

            AwakeTime = -2.5f - Rand.Float(0.5f);

            _cellVisitTimes = new TimeSince[Game.CurrentMaze.Rows, Game.CurrentMaze.Cols];

            OnLevelChange();
        }

        if (!IsInBounds(cell))
        {
            return;
        }

        _cellVisitTimes[cell.Row, cell.Col] = 0f;

        OnServerTick();
    }

    protected virtual void OnLevelChange()
    {

    }

    protected virtual void OnServerTick()
    {
        var cell = this.GetCellIndex();

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

        //DebugOverlay.Text(Velocity.Length.ToString(), EyePosition, 0f, float.MaxValue);

        Controller?.Simulate(default, this, null);
        Animator?.Simulate(default, this, null);

        if ( LastAttack < 1f || !EnableDrawing )
        {
            return;
        }

        if ( AwakeTime < 0f )
        {
            Animator?.SetAnimParameter( "holdtype", 0 );
            return;
        }

        Animator?.SetAnimParameter( "holdtype", HoldType );

        var closestPlayer = Game.GetClosestPlayer( Position, KillRange );

        if ( closestPlayer != null && !closestPlayer.IsVaulting )
        {
            LastAttack = 0f;

            //Animator?.SetAnimParameter("b_vr", false);
            Animator?.SetAnimParameter("holdtype", 5);
            Animator?.Trigger( "b_attack" );

            closestPlayer.Kill( (closestPlayer.Position - Position).WithZ( 0f ), DeathMessage );
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

    public float GetCost( GridCoord coord )
    {
        if ( coord != Game.ExitCell )
        {
            return 0f;
        }

        if ( Game.Hatch?.IsOpen ?? true )
        {
            return 0f;
        }

        return float.PositiveInfinity;
    }

    protected virtual void OnReachTarget()
    {

    }

    protected GridCoord GetRandomNeighborCell()
    {
        var cell = this.GetCellIndex();

        var dir = MazeData.Directions.Where( x => CanWalkInDirection( x.Direction ) )
            .OrderBy( x => Rand.Float() - GetSinceLastVisited( cell + x.Delta ) + GetCost( cell + x.Delta ) )
            .FirstOrDefault();

        return cell + dir.Delta;
    }
    
    private PathFinder _pathFinder;
    private readonly List<GridCoord> _path = new();

    protected GridCoord GetNextInPathTo( Vector3 pos )
    {
        var cell = Game.PositionToCellIndex( pos );

        if ( cell != this.GetCellIndex() )
        {
            return GetNextInPathTo( cell );
        }

        var cellFrac = Game.PositionToCell( pos );

        cellFrac.Row -= cell.Row + 0.5f;
        cellFrac.Col -= cell.Col + 0.5f;

        if ( Math.Abs( cellFrac.Row ) > Math.Abs( cellFrac.Col ) )
        {
            return cell + (Math.Sign( cellFrac.Row ), 0);
        }
        else
        {
            return cell + (0, Math.Sign(cellFrac.Col));
        }
    }

    protected GridCoord GetNextInPathTo( GridCoord coord )
    {
        var cell = this.GetCellIndex();

        _pathFinder ??= new PathFinder();
        _path.Clear();

        if ( !_pathFinder.FindPath( cell, coord, _path ) )
        {
            return GetRandomNeighborCell();
        }

        if ( _path.Count < 2 )
        {
            return cell;
        }

        return _path.Skip( 1 ).First();
    }

    protected void AddClothingItem(string itemName)
    {
        if (ResourceLibrary.TryGet(itemName, out Clothing item))
        {
            Clothing.Clothing.Add(item);
        }
        else
        {
            Log.Error("Couldn't find clothing: " + itemName);
        }
    }
}
