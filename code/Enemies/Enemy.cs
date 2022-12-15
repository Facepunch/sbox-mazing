using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Player;
using Sandbox;
using Sandbox.Diagnostics;

namespace Mazing.Enemies;

/// <summary>
/// Which level does this enemy first appear.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class UnlockLevelAttribute : Attribute
{
    public int Level { get; }

    public UnlockLevelAttribute( int level )
    {
        Level = level;
    }
}

/// <summary>
/// How dangerous is this enemy compared to a <see cref="Wanderer"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ThreatValueAttribute : Attribute
{
    public int Value { get; }

    public int SpawnCount { get; }

    public ThreatValueAttribute( int value, int spawnCount = 1 )
    {
        Value = value;
        SpawnCount = spawnCount;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class CantBeOnlyEnemyAttribute : Attribute
{

}

/// <summary>
/// Specify that this enemy type replaces another one.
/// The replacement will start after this type's <see cref="UnlockLevelAttribute"/>,
/// starting with 1 instance being replaced, until <see cref="LevelsUntilFullyReplaced"/>
/// is reached. At that point, the replaced type will not occur at all.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ReplacesAttribute : Attribute
{
    /// <summary>
    /// Which type should be replaced by this enemy type.
    /// </summary>
    public Type ReplacedType { get; }
    
    /// <summary>
    /// How many levels after this type first unlocks will the replaced type be
    /// 100% replaced.
    /// </summary>
    public int LevelsUntilFullyReplaced { get; }
    
    public ReplacesAttribute( Type replacedType, int levelsUntilFullyReplaced )
    {
        ReplacedType = replacedType;
        LevelsUntilFullyReplaced = levelsUntilFullyReplaced;
    }
}

public abstract partial class Enemy : AnimatedEntity
{
    public const float KillRange = 12f;

    public virtual float MoveSpeed => 100f;

    public bool IsAlive { get; private set; } = true;

    public PawnController Controller { get; set; }

    public GridCoord TargetCell { get; set; }

    public MazingGame Game => MazingGame.Current;
    
    private TimeSince[,] _cellVisitTimes;

    public TimeSince LastAttack { get; private set; }

    public TimeSince AwakeTime { get; set; }

    public bool IsAwake => AwakeTime > 0f;

    private float _wakeDelay;

    public int Index { get; private set; }

    public ClothingContainer Clothing { get; set; } = new();

    private bool _firstRandomTarget = true;

    private static readonly (string Verb, float Weight)[] _sDeathVerbs = new (string Verb, float Weight)[]
    {
        ("killed", 25f),
        ("slain", 3f),
        ("murdered", 2f),
        ("eliminated", 2f),
        ("defeated", 2f),
        ("slapped", 1f),
        ("wasted", 1f),
        ("thwarted", 1f),
        ("slaughtered", 1f),
        ("whacked", 0.5f),
        ("taken out", 0.5f),
        ("put down", 0.5f),
        ("assassinated", 0.5f),
        ("humiliated", 0.5f),
    };

    private static string GetRandomDeathVerb()
    {
        return MazingGame.GetRandomWeighted(_sDeathVerbs);
    }

    [ConCmd.Client("mazing_deathtest")]
    public static void TestDeathVerbs()
    {
        var verbs = Enumerable.Range(0, 1000)
            .Select(x => GetRandomDeathVerb())
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ToArray();

        foreach (var verb in verbs)
        {
            Log.Info($"{verb.Key}: {verb.Count()}");
        }
    }

    //protected virtual string DeathMessage => $"{{0}} was {GetRandomDeathVerb()} by a {GetType().Name.ToTitleCase()}";
    protected virtual string DeathMessage => $"{{0}} was {GetRandomDeathVerb()} by {NounPhrase}";
    public virtual string NounPhrase => "an enemy";

    protected virtual int HoldType => 5;

    public virtual Vector3 LookPos =>
        AimRay.Position + AimRay.Forward * 200 + Vector3.Up * Math.Clamp( AwakeTime, -1f, 0f ) * 800f;

    public override void Spawn()
    {
        base.Spawn();

        SetModel(ModelPath);

        Controller = OnCreateController();

        Tags.Add( "enemy" );

        EnableAllCollisions = true;
        EnableDrawing = true;
        EnableHideInFirstPerson = true;
        EnableShadowInFirstPerson = true;

        TargetCell = this.GetCellIndex();
    }

    public void PostSpawn( int index, Vector3 pos )
    {
        Index = index;
        Position = pos;

        OnPostSpawn();
    }

    protected virtual void OnPostSpawn()
    {
        var direction = MazeData.Directions
            .MinBy(x => (CanWalkInDirection(x.Direction) ? 0f : 10f) + Sandbox.Game.Random.Float());

        Rotation = Rotation.LookAt(direction.Delta.Normal, Vector3.Up);
    }

    protected virtual string ModelPath => "models/citizen/citizen.vmdl";

    protected virtual PawnController OnCreateController()
    {
        return new MazingWalkController
        {
            DefaultSpeed = MoveSpeed
        };
    }
    
    private TimeSince _lastFootstep;

    public override void OnAnimEventFootstep( Vector3 pos, int foot, float volume )
    {
        base.OnAnimEventFootstep(pos, foot, volume);

        if (GroundEntity != null && Sandbox.Game.IsClient && _lastFootstep > 0.25f)
        {
            _lastFootstep = 0f;
            var sound = Sound.FromWorld("player.footstep", pos);
            sound.SetVolume(Math.Clamp(Velocity.Length / 160f, 0.25f, 1f));
        }
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
    
    public void ServerTick()
    {
        var cell = this.GetCellIndex();

        if (_cellVisitTimes == null)
        {
            TargetCell = cell;

            _wakeDelay = 2.5f + Sandbox.Game.Random.Float(0.5f);
            AwakeTime = -_wakeDelay;

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

    protected virtual void OnHandleMovement( MazingWalkController walkController )
    {
        var cell = this.GetCellIndex();

        if (AwakeTime > 0f && !walkController.IsVaulting)
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

        var otherEnemy = Game.GetClosestEnemy(Position, 32f, except: this);

        walkController.DefaultSpeed = otherEnemy == null || otherEnemy.MoveSpeed < MoveSpeed || otherEnemy.Index > Index
            ? MoveSpeed
            : MoveSpeed * 0.5f;

        var dir = Game.CellToPosition(TargetCell.Row + 0.5f, TargetCell.Col + 0.5f) - Position;

        //DebugOverlay.Box(Game.CellToPosition(TargetCell.Row, TargetCell.Col),
        //    Game.CellToPosition(TargetCell.Row + 1f, TargetCell.Col + 1f),
        //    new Color(0f, 1f, 0f, 0.1f), depthTest: false);

        walkController.InputVector = dir;
    }

    protected virtual void OnServerTick()
    {
        if (Controller is MazingWalkController walkController)
        {
            OnHandleMovement(walkController);
        }

        //DebugOverlay.Text(Velocity.Length.ToString(), EyePosition, 0f, float.MaxValue);

        Controller?.Simulate(null, this);

        if ( LastAttack < 1f || !EnableDrawing )
        {
            return;
        }

        if ( AwakeTime < 0f )
        {
            // TODO: Animator?.SetAnimParameter( "holdtype", 0 );
            return;
        }

        // TODO: Animator?.SetAnimParameter( "holdtype", HoldType );

        var closestPlayer = Game.GetClosestPlayer( Position, KillRange, ignoreZ: false );

        if ( closestPlayer != null && !closestPlayer.IsVaulting )
        {
            LastAttack = 0f;

            // TODO: Animator?.SetAnimParameter("b_vr", false);
            // TODO: Animator?.SetAnimParameter("holdtype", 5);
            // TODO: Animator?.Trigger( "b_attack" );

            Sound.FromEntity( "enemy.punch", this );

            closestPlayer.Kill( (closestPlayer.Position - Position).WithZ( 0f ), DeathMessage, this );
        }
    }

    public void Kill(Vector3 damageDir, bool ragdoll = true)
    {
        if (!Sandbox.Game.IsServer || !IsAlive)
        {
            return;
        }

        IsAlive = false;

        if (ragdoll && ModelPath == "models/citizen/citizen.vmdl")
        {
            var ragdollEnt = new AnimatedEntity();

            ragdollEnt.SetModel(ModelPath);
            ragdollEnt.Position = Position;
            ragdollEnt.Rotation = Rotation;
            ragdollEnt.SetupPhysicsFromModel(PhysicsMotionType.Dynamic, false);
            ragdollEnt.PhysicsGroup.Velocity = damageDir.Normal * 100f;
            ragdollEnt.Tags.Add("ragdoll");

            Clothing.DressEntity(ragdollEnt);
        }

        OnKilled();
    }

    [Event.Tick.Client]
    public void ClientTick()
    {
        OnClientTick();
    }

    protected virtual void OnClientTick()
    {

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
        return IsInBounds(coord) && _cellVisitTimes != null ? _cellVisitTimes[coord.Row, coord.Col] : default;
    }

    public float GetCost( GridCoord coord )
    {
        if ( coord != Game.ExitCell )
        {
            return 0f;
        }

        if ( !(Game.Hatch?.IsOpen ?? false) )
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

        if ( _firstRandomTarget )
        {
            _firstRandomTarget = false;

            // Try just walking forwards after spawning, to not surprise players

            if ( CanWalkInDirection( this.GetFacingDirection() ) )
            {
                return cell + this.GetFacingDirection();
            }
        }

        var dir = MazeData.Directions.Where( x => CanWalkInDirection( x.Direction ) )
            .OrderBy( x => Sandbox.Game.Random.Float() - GetSinceLastVisited( cell + x.Delta ) + GetCost( cell + x.Delta ) )
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

    protected GridCoord GetNextInPathTo( GridCoord coord, GridCoord? from = null )
    {
        var cell = from ?? this.GetCellIndex();

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

    public int GetPathLengthTo( Vector3 pos, GridCoord? from = null )
    {
        var cell = from ?? this.GetCellIndex();
        var targetCell = Game.PositionToCellIndex(pos);

        _pathFinder ??= new PathFinder();
        _path.Clear();

        if (!_pathFinder.FindPath(cell, targetCell, _path))
        {
            return 99999;
        }

        return _path.Count;
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
