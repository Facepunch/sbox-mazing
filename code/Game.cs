using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mazing.Enemies;
using Mazing.Items;
using Mazing.Player;
using Mazing.UI;
using Sandbox.UI;

namespace Mazing;

/// <summary>
/// This is your game class. This is an entity that is created serverside when
/// the game starts, and is replicated to the client. 
/// 
/// You can use this to create things like HUDs and declare which player class
/// to use for spawned players.
/// </summary>
public partial class MazingGame : Sandbox.Game
{
    public const int MaxPlayers = 8;

	public new static MazingGame Current => Game.Current as MazingGame;

	[Net]
	public MazeData CurrentMaze { get; set; }

	[Net]
    public (int Row, int Col) ExitCell { get; set; }

	[Net, HideInEditor]
	public int LevelIndex { get; set; }

    [Net]
    public int TotalTreasureValue { get; set; }

    [Net, HideInEditor]
    public int NextLevelIndex { get; set; }

    [Net, HideInEditor]
    public int TotalCoins { get; set; }

    [Net] public TimeSince RestartCountdown { get; set; }

    [Net] public TimeSince NextLevelCountdown { get; set; }

    [Net]
    public Key Key { get; set; }

    [Net]
    public Hatch Hatch { get; set; }

    [Net]
    public bool IsEditorMode { get; set; }

    private bool _hasCheated;

    private int _nextLevelSeed = -1;

    private readonly HashSet<Entity> _worldEntities = new();
    private bool _firstSpawn = true;

    private readonly List<Enemy> _enemies = new();

    private readonly Dictionary<TypeDescription, int> _enemyTypeLastSpawnLevel
        = new Dictionary<TypeDescription, int>();

    public IEnumerable<MazingPlayer> Players => Client.All
        .Select( x => x.Pawn )
        .OfType<MazingPlayer>();

    public IEnumerable<MazingPlayer> PlayersAliveInMaze => Players
        .Where( x => x.IsAliveInMaze );

    public IEnumerable<Enemy> Enemies => _enemies;

    public IEnumerable<Treasure> Treasure => Entity.All.OfType<Treasure>();

    public bool IsTransitioning => RestartCountdown > -1f && RestartCountdown < 0f ||
                                   NextLevelCountdown > -1f && NextLevelCountdown < 0f;

    private readonly Dictionary<(GridCoord Coord, Direction Dir), ModelEntity> _walls = new();
    private GridCoord[] _playerSpawns;

	public MazingGame()
	{
        if ( IsClient )
        {
            new HudRoot();
        }
        else
        {
            IsEditorMode = Host.IsToolsEnabled;
        }

        RestartCountdown = float.PositiveInfinity;
        NextLevelCountdown = float.PositiveInfinity;
    }

    [ConCmd.Admin( "mazing_level", Help = "Go to a given level" )]
    public static void GoToLevel( int level, string seed = null )
    {
        if (!string.IsNullOrEmpty( seed ) && int.TryParse(seed, NumberStyles.HexNumber, null, out var seedInt))
        {
            Current._nextLevelSeed = seedInt;
        }
        else
        {
            Current._nextLevelSeed = -1;
        }

        Current._hasCheated = true;
        Current.NextLevelIndex = Math.Max( level - 1, 0 );
        Current.NextLevelCountdown = -1.5f;
    }

    private record struct EnemyType(
        TypeDescription Type,
        int FirstLevel,
        int Threat,
        int SpawnCount,
        bool CanBeOnlyEnemy,
        TypeDescription Replaces,
        int FullyReplaceLevel );

    [ConCmd.Client("mazing_spawntest")]
    public static void EnemyTypeTest()
    {
        var lastSpawnLevels = new Dictionary<TypeDescription, int>();

        for (var i = 0; i < 50; ++i)
        {
            var seed = Rand.Int(int.MaxValue - 1);
            var types = GetSpawningEnemyTypes(i, seed, lastSpawnLevels)
                .GroupBy( x => x )
                .OrderByDescending(x => x.Count())
                .Select( x => $"{x.Key.Name} x{x.Count()}")
                .ToArray();

            Log.Info($"Level {i + 1}: {string.Join(", ", types)}");
        }
    }

    public static IEnumerable<TypeDescription> GetSpawningEnemyTypes( int levelIndex, int seed, Dictionary<TypeDescription, int> lastSpawnLevels )
    {
        var rand = new Random( seed );

        var totalThreat = levelIndex == 0 ? 1 : levelIndex + 2;
        var levelNumber = levelIndex + 1;

        var unlocked = TypeLibrary.GetDescriptions<Enemy>()
            .Select( x =>
            {
                var threatAttrib = x.GetAttribute<ThreatValueAttribute>();
                var replacesAttrib = x.GetAttribute<ReplacesAttribute>();

                var firstLevel = x.GetAttribute<UnlockLevelAttribute>()?.Level ?? int.MaxValue;
                
                return new EnemyType(
                    Type: x,
                    FirstLevel: firstLevel,
                    Threat: threatAttrib?.Value ?? 1,
                    SpawnCount: threatAttrib?.SpawnCount ?? 1,
                    CanBeOnlyEnemy: x.GetAttribute<CantBeOnlyEnemyAttribute>() == null,
                    Replaces: replacesAttrib?.ReplacedType != null ? TypeLibrary.GetDescription( replacesAttrib.ReplacedType ) : null,
                    FullyReplaceLevel: firstLevel == int.MaxValue ? int.MaxValue : firstLevel + replacesAttrib?.LevelsUntilFullyReplaced ?? 0);
            } )
            .Where( x => x.FirstLevel <= levelNumber)
            .ToArray();

        var replacedBy = unlocked
            .Where(x => x.Replaces != null)
            .ToDictionary(x => x.Replaces, x => x);

        var justUnlocked = unlocked
            .Where( x => x.FirstLevel == levelNumber)
            .ToArray();

        var alreadyUnlocked = unlocked
            .Where( x => x.FirstLevel < levelNumber && !replacedBy.ContainsKey(x.Type) )
            .OrderByDescending( x => rand.NextSingle() * Math.Max(1, lastSpawnLevels.TryGetValue(x.Type, out var lastLevel) ? levelIndex + 1 - lastLevel : 1))
            .ToArray();

        // Make sure first enemy in list can be the only enemy in the level
        var canBeOnlyIndex = Array.FindIndex( alreadyUnlocked, x => x.CanBeOnlyEnemy );

        if ( canBeOnlyIndex > 0 )
        {
            (alreadyUnlocked[0], alreadyUnlocked[canBeOnlyIndex]) = (alreadyUnlocked[canBeOnlyIndex], alreadyUnlocked[0]);
        }

        // Choose which types of enemies will spawn:
        // * Any that have just unlocked are guaranteed to spawn
        // * Pick at least one other type too, if possible

        var canBeOnlyJustUnlocked = justUnlocked.Length > 1
            || justUnlocked.Length == 1 && justUnlocked[0].CanBeOnlyEnemy
            || alreadyUnlocked.Length == 0;

        var totalCount = canBeOnlyJustUnlocked
            ? rand.Next(justUnlocked.Length, Math.Max(justUnlocked.Length, 3) + 1)
            : rand.Next(justUnlocked.Length + 1, Math.Max(justUnlocked.Length + 1, 3) + 1);

        if (unlocked.Length > 1 && rand.NextSingle() < 0.8f)
        {
            totalCount = Math.Max(2, totalCount);
        }

        var usedTypes = justUnlocked
            .Concat( alreadyUnlocked.Take(totalCount - justUnlocked.Length) )
            .Select(x => (x.Type, x.Threat, x.SpawnCount, x.Replaces,
                Weight: x.Replaces == null || x.FullyReplaceLevel <= levelNumber ? 1f
                : (levelNumber - x.FirstLevel + 1f) / (x.FullyReplaceLevel - x.FirstLevel + 1)))
            .ToList();

        for (var i = usedTypes.Count - 1; i >= 0; --i)
        {
            var replacer = usedTypes[i];
            if (replacer.Replaces == null || replacer.Weight >= 1f) continue;

            var replaced = unlocked.FirstOrDefault(x => x.Type == replacer.Replaces);
            if (replaced.Type == null) continue;

            usedTypes.Add((replaced.Type, replaced.Threat, replaced.SpawnCount, null, Weight: 1f - replacer.Weight));
        }
        
        var spawned = new List<TypeDescription>();

        // Make sure at least one enemy of each chosen type spawns
        for ( var i = 0; totalThreat > 0 && i < usedTypes.Count; ++i )
        {
            var type = usedTypes[i];

            if ( type.Threat > totalThreat )
            {
                continue;
            }

            totalThreat -= type.Threat;
            lastSpawnLevels[type.Type] = levelIndex;

            for ( var j = 0; j < type.SpawnCount; ++j )
            {
                spawned.Add(type.Type);
            }
        }

        // Spawn other enemies until the total threat is reached
        while ( totalThreat > 0 )
        {
            var next = usedTypes
                .Where(x => x.Threat <= totalThreat)
                .OrderBy(x => spawned.Count(y => y == x.Type) * x.Threat * x.SpawnCount / x.Weight)
                .FirstOrDefault();

            if (next.Type == null)
            {
                next = usedTypes.MinBy(x => x.Threat + Rand.Float() * 0.5f);
            }

            totalThreat -= next.Threat;
            lastSpawnLevels[next.Type] = levelIndex;

            for (var j = 0; j < next.SpawnCount; ++j)
            {
                spawned.Add(next.Type);
            }
        }

        return spawned;
    }

    private static float GetLevelSizeScore( int length, int cross, int targetArea )
    {
        var area = length * cross;

        if ( area < targetArea )
        {
            return 0f;
        }

        var max = Math.Max( length, cross );
        var min = Math.Min( length, cross );

        return (float)targetArea * targetArea / (area * area) * min * min * min / (max * max * max);
    }

    public (int Rows, int Cols) GetLevelSize( int levelIndex, int seed )
    {
        var rand = new Random( seed );
        var targetArea = 64 + (levelIndex < 2 ? 0 : (MathF.Sqrt(levelIndex - 1f) * 10f).FloorToInt());
        var minLength = 4;
        var maxLength = MathF.Sqrt(targetArea).FloorToInt();

        var candidates = new List<(int Length, int Cross, float Score)>();
        var totalScore = 0f;

        for ( var length = minLength; length <= maxLength; length += 4 )
        {
            var cross = (((float)targetArea / length) / 4f).CeilToInt() * 4;
            var score = GetLevelSizeScore( length, cross, targetArea );

            candidates.Add( (length, cross, score) );
            totalScore += score;
        }

        var targetScore = rand.NextSingle() * totalScore;

        foreach ( var (length, cross, score) in candidates )
        {
            targetScore -= score;

            if ( targetScore < 0f )
            {
                return rand.NextSingle() < 0.25f ? (cross, length) : (length, cross);
            }
        }

        throw new Exception();
    }

    public bool ShouldRemoveBetweenLevels( Entity ent )
    {
        if ( ent.Parent?.IsValid ?? false )
        {
            return false;
        }

        if ( ent.GetType().ToString() == "Sandbox.ClientEntity" )
        {
            return false;
        }

        switch ( ent )
        {
            case MazingPlayer:
                return false;
        }

        return !_worldEntities.Contains( ent );
    }

	public void GenerateMaze( int seed = -1 )
	{
		Host.AssertServer();

        if ( LevelIndex == 0 )
        {
            GameServices.StartGame();
        }

        foreach (var pair in _enemyTypeLastSpawnLevel.ToArray())
        {
            if (pair.Value > LevelIndex)
            {
                _enemyTypeLastSpawnLevel[pair.Key] = LevelIndex;
            }
        }

        if ( _firstSpawn )
        {
            _firstSpawn = false;

            foreach ( var entity in Entity.All )
            {
                switch ( entity )
                {
                    case MazingPlayer:
                        continue;
                }

                _worldEntities.Add( entity );
            }
        }

        var entitiesToDelete = Entity.All
            .Where( ShouldRemoveBetweenLevels )
            .ToArray();

        foreach ( var entity in entitiesToDelete )
        {
            entity.Delete();
        }

        _walls.Clear();
        _enemies.Clear();

        if ( seed == -1 )
        {
            seed = Rand.Int(1, int.MaxValue - 1);
        }

		Log.Info( $"Generating level {LevelIndex + 1} with seed {seed:x8} ");

        var typesToSpawn = GetSpawningEnemyTypes( LevelIndex, seed, _enemyTypeLastSpawnLevel )
            .ToArray();
        
        var (rows, cols) = GetLevelSize( LevelIndex, seed );
        
        var generated = LevelIndex == 0
            ? MazeGenerator.GenerateLobby()
            : MazeGenerator.Generate( seed, rows, cols, MaxPlayers, typesToSpawn.Length,
                LevelIndex * 2 + 2 );

        CurrentMaze = generated.MazeData;
        CurrentMaze.WriteNetworkData();

        _playerSpawns = generated.Players;
        
        var enemies = typesToSpawn.Select(x => x.Create<Enemy>())
            .ToArray();

        ExitCell = generated.Exit;

        const float outerWallHeight = 128;
		const float innerWallHeight = 96f;
        const float wallModelHeight = 256f;
        const float borderHeight = outerWallHeight - 16f;

        Hatch = new Hatch();

        var lightCount = (rows * cols / 32f).CeilToInt();
        var lights = new List<PointLightEntity>();

        var minLightDist = 128f;

        var hueA = Rand.Float( 0f, 360f );
        var hueB = hueA + 180f;

        for ( var i = 0; i < lightCount; ++i )
        {
            var valid = true;
            Vector3 pos = default;

            for ( var attempt = 0; attempt < 100; ++attempt )
            {
                pos = this.CellCenterToPosition( (Rand.Int( 2, rows - 3 ), Rand.Int( 2, cols - 3 )) );

                valid = true;

                foreach ( var other in lights )
                {
                    if ( (other.Position - pos).WithZ( 0f ).LengthSquared <= minLightDist * minLightDist )
                    {
                        valid = false;
                        break;
                    }
                }

                if ( valid ) break;
            }

            if ( !valid ) break;

            var light = new PointLightEntity
            {
                Range = 256f,
                Color = new ColorHsv( (i & 1) == 0 ? hueA : hueB, 0.25f, 1f ),
                Brightness = 4f,
                Position = pos + Vector3.Up * 128f
            };

            lights.Add( light );
        }

        for ( var i = 0; i < enemies.Length; i++ )
        {
            var enemyCell = generated.Enemies[i % generated.Enemies.Length];
            
            enemies[i].PostSpawn( i, CellToPosition( enemyCell.Row + 0.5f, enemyCell.Col + 0.5f ) );

            _enemies.Add( enemies[i] );
        }

        TotalTreasureValue = generated.Coins.Length * Items.Treasure.GetValue( TreasureKind.Emerald );

        var possibleKinds = new List<TreasureKind>();
        var treasureIndex = 0;

        var totalTreasureValue = TotalTreasureValue;

        while ( totalTreasureValue > 0 )
        {
            possibleKinds.Clear();

            foreach ( var kind in Enum.GetValues<TreasureKind>() )
            {
                if ( Items.Treasure.GetValue( kind ) <= totalTreasureValue )
                {
                    possibleKinds.Add( kind );
                }
            }

            var chosenKind = possibleKinds[Rand.Int( possibleKinds.Count - 1 )];
            var coinCell = generated.Coins[treasureIndex++];

            new Treasure( chosenKind )
            {
                Position = CellToPosition( coinCell.Row + 0.5f, coinCell.Col + 0.5f )
            };

            totalTreasureValue -= Items.Treasure.GetValue( chosenKind );
        }
        
        Key = new Key
        {
            Position = CellToPosition( generated.Key.Row + 0.5f, generated.Key.Col + 0.5f ) + Vector3.Up * 64f
        };

		for (var row = 0; row <= CurrentMaze.Rows; row++)
		{
			for (var col = 0; col <= CurrentMaze.Cols; col++)
			{
				if (row < CurrentMaze.Rows && CurrentMaze.GetWall((row, col), Direction.West))
                {
                    var height = col <= 0 || col >= CurrentMaze.Cols ? outerWallHeight : innerWallHeight;
                    var wall = new Wall
                    {
                        Position = CellToPosition( row + 1f, col ) + Vector3.Up * (height - wallModelHeight)
                    };

                    _walls.Add(((row, col), Direction.West), wall);

                }

				if (col < CurrentMaze.Cols && CurrentMaze.GetWall((row, col), Direction.South))
				{
                    var height = row <= 0 || row >= CurrentMaze.Rows ? outerWallHeight : innerWallHeight;
                    var wall = new Wall
                    {
                        Position = CellToPosition( row, col ) + Vector3.Up * (height - wallModelHeight),
                        Rotation = Rotation.FromYaw( 90f )
                    };

                    _walls.Add(((row, col), Direction.South), wall);
                }

				var north = CurrentMaze.GetWall((row - 1, col), Direction.West);
				var south = CurrentMaze.GetWall((row, col), Direction.West);

				var west = CurrentMaze.GetWall((row, col - 1), Direction.South);
				var east = CurrentMaze.GetWall((row, col), Direction.South);

				if (north != south || west != east || north && west)
				{
                    var height = row <= 0 || row >= CurrentMaze.Rows || col <= 0 || col >= CurrentMaze.Cols
                        ? outerWallHeight : innerWallHeight;

                    new Post
                    {
                        Position = CellToPosition( row, col ) + Vector3.Up * (height - wallModelHeight)
                    };
                }
			}
		}

        new Border
        {
            Position = Vector3.Up * borderHeight + CellToPosition( CurrentMaze.Rows, 0f )
        };

        new Border
        {
            Position = Vector3.Up * borderHeight + CellToPosition( 0f, CurrentMaze.Cols ),
            Rotation = Rotation.FromYaw( 180f )
        };
    }

	public Vector3 CellToPosition( float row, float col ) => new Vector3( (col - ExitCell.Col - 0.5f) * 48f, (row - ExitCell.Row - 0.5f) * 48f, 0f );
    public Vector3 CellToPosition( GridCoord coord ) => new Vector3((coord.Col - ExitCell.Col - 0.5f) * 48f, (coord.Row - ExitCell.Row - 0.5f) * 48f, 0f);

    public Vector3 CellCenterToPosition(GridCoord coord) => CellToPosition(coord) + new Vector3(24f, 24f, 0f);

    public Vector3 GetCellCenter( Vector3 position )
    {
        var (row, col) = PositionToCell( position );

        return CellToPosition( row.FloorToInt() + 0.5f, col.FloorToInt() + 0.5f );
    }

    private T GetClosest<T>( IEnumerable<T> enumerable, Vector3 pos, float maxRange, bool ignoreZ, T except )
        where T : Entity
    {
        var dists = ignoreZ
            ? enumerable.Select(x => (Entity: x, DistSq: (x.Position - pos).WithZ(0f).LengthSquared))
            : enumerable.Select(x => (Entity: x, DistSq: (x.Position - pos).LengthSquared));

        return dists.OrderBy( x => x.DistSq )
            .FirstOrDefault( x => x.DistSq <= maxRange * maxRange && x.Entity != except && (!ignoreZ || x.Entity.Parent == null) )
            .Entity;
    }

    public MazingPlayer GetClosestPlayer( Vector3 pos, float maxRange = float.PositiveInfinity, bool aliveInMaze = true, bool ignoreZ = true, MazingPlayer except = null )
    {
        var players = aliveInMaze ? PlayersAliveInMaze : Players;
        return GetClosest( players, pos, maxRange, ignoreZ, except );
    }

    public MazingPlayer GetClosestDeadPlayer(Vector3 pos, float maxRange = float.PositiveInfinity, bool ignoreZ = true, MazingPlayer except = null)
    {
        var players = Players.Where(x => !x.IsAliveInMaze);
        return GetClosest(players, pos, maxRange, ignoreZ, except);
    }

    public Enemy GetClosestEnemy( Vector3 pos, float maxRange = float.PositiveInfinity, bool ignoreZ = true, Enemy except = null )
    {
        return GetClosest( Enemies, pos, maxRange, ignoreZ, except );
    }

    public (float Row, float Col) PositionToCell( Vector3 pos ) =>
        (pos.y / 48f + ExitCell.Row + 0.5f, pos.x / 48f + ExitCell.Col + 0.5f);

    public GridCoord PositionToCellIndex( Vector3 pos )
    {
        var (row, col) = PositionToCell( pos );
        return new GridCoord( row.FloorToInt(), col.FloorToInt() );
    }

    public bool IsPlayerInCell( GridCoord coord )
    {
        // TODO: optimize

        return PlayersAliveInMaze
            .Any( x => x.GetCellIndex() == coord );
    }

    public MazingPlayer GetPlayerInCell(GridCoord coord)
    {
        return PlayersAliveInMaze
            .FirstOrDefault(x => x.GetCellIndex() == coord);
    }

    public bool IsEnemyInCell(GridCoord coord)
    {
        // TODO: optimize
        return Enemies
            .Any( x => x.GetCellIndex() == coord );
    }

    public GridCoord GetRandomEmptyCell()
    {
        for ( var i = 0; i < 100; ++i )
        {
            var cell = new GridCoord( Rand.Int( 0, CurrentMaze.Rows - 1 ), Rand.Int( 0, CurrentMaze.Cols - 1 ) );

            if ( cell != ExitCell )
            {
                return cell;
            }
        }

        // Give up
        return new GridCoord(Rand.Int(0, CurrentMaze.Rows - 1), Rand.Int(0, CurrentMaze.Cols - 1));
    }

    public bool IsInMaze(GridCoord cell)
    {
        if (cell.Col < 0 || cell.Row < 0 || cell.Col >= CurrentMaze.Cols || cell.Row >= CurrentMaze.Rows)
            return false;

        return true;
    }

    [ClientRpc]
    private void ClientJoinNotify( string name, long playerId )
    {
        ChatBox.AddInformation($"{name} has entered the maze", $"avatar:{playerId}");
    }

    /// <summary>
    /// A client has joined the server. Make them a pawn to play with
    /// </summary>
    public override void ClientJoined( Client client )
    {
        Log.Info( $"\"{client.Name}\" has joined the game" );

        if ( CurrentMaze == null )
		{
			GenerateMaze();
		}

		// Create a pawn for this client to play with
		var mazingPlayer = new MazingPlayer( client );

        client.Pawn = mazingPlayer;

        mazingPlayer.FirstSeenLevelIndex = LevelIndex;

        RespawnPlayer( mazingPlayer );

        if ( LevelIndex > 0 )
        {
            mazingPlayer.Kill( Vector3.Up, "{0} has joined as a ghost", this, false );
        }
        else
        {
            ClientJoinNotify( client.Name, client.PlayerId );
        }
    }

    private void RespawnPlayer( MazingPlayer player )
    {
        player.HasExited = false;

        player.Respawn();
	}

    public override void MoveToSpawnpoint( Entity pawn )
    {
        if ( pawn is MazingPlayer player )
        {
            var index = Array.IndexOf(Players.ToArray(), player);

            // Spawn in a random grid cell
            var spawnCell = _playerSpawns[index % _playerSpawns.Length];

            player.Position = CellToPosition(spawnCell.Row + 0.5f, spawnCell.Col + 0.5f) + Vector3.Up * 1024f;
        }
    }

    public override void DoPlayerNoclip( Client player )
    {
        Log.Info( $"Noclip is disabled" );
    }

    public override void DoPlayerSuicide( Client cl )
    {
        Log.Info( $"Suicide is disabled" );
    }

    [Event.Tick.Server]
    public void ServerTick()
    {
        foreach ( var enemy in Enemies )
        {
            enemy.ServerTick();
        }

        foreach ( var treasure in Treasure )
        {
            treasure.ServerTick();
        }

        if ( !float.IsPositiveInfinity( RestartCountdown ) )
        {
            if ( RestartCountdown > 0f )
            {
                RestartCountdown = float.PositiveInfinity;

                LevelIndex = 0;
                TotalCoins = 0;

                GameServices.EndGame();

                _hasCheated = false;
                
                GenerateMaze();
                ResetPlayers();
            }

            return;
        }

        if ( !float.IsPositiveInfinity( NextLevelCountdown ) )
        {
            if ( NextLevelCountdown > 0f )
            {
                NextLevelCountdown = float.PositiveInfinity;

                LevelIndex = NextLevelIndex;

                if ( !_hasCheated && !Host.IsToolsEnabled )
                {
                    foreach (var player in All.OfType<MazingPlayer>())
                    {
                        if ( player.Client == null ) continue;
                        if ( LevelIndex < player.FirstSeenLevelIndex * 2 )
                        {
                            Log.Info( $"Not submitting score for {player.Client.Name}" );
                        }

                        GameServices.UpdateLeaderboard(player.Client.PlayerId, TotalCoins, "money");
                        GameServices.UpdateLeaderboard(player.Client.PlayerId, LevelIndex, "depth");
                    }
                }

                GenerateMaze( _nextLevelSeed );
                ResetPlayers();

                _nextLevelSeed = -1;
            }

            return;
        }

        var allExited = true;
        var anyPlayers = false;
        var anyDeadPlayers = false;

        foreach ( var player in Players )
        {
            if ( !player.IsAlive )
            {
                anyDeadPlayers = true;
                continue;
            }

            anyPlayers = true;

            if ( !player.HasExited )
            {
                allExited = false;
				break;
            }
        }

        if ( anyPlayers && allExited )
        {
            NextLevelCountdown = -1.5f;
            NextLevelIndex = LevelIndex + 1;
        }
        else if ( !anyPlayers && anyDeadPlayers )
        {
            RestartCountdown = -3f;
            ClientNotifyFinalScore( TotalCoins );
        }
    }
    
    [ClientRpc]
    public void ClientNotifyFinalScore( int score )
    {
        ChatBox.AddInformation( $"Everyone is dead! Final score: ${score}" );
    }

    public void DestroyWall( GridCoord coord, Direction dir )
    {
        if ( !IsServer )
        {
            return;
        }

        if ( !CurrentMaze.GetWall( coord, dir ) )
        {
            return;
        }

        CurrentMaze.SetWall( coord, dir, false );

        if ( dir == Direction.East )
        {
            dir = Direction.West;
            coord += (0, 1);
        }
        else if ( dir == Direction.North )
        {
            dir = Direction.South;
            coord += (1, 0);
        }

        if ( _walls.TryGetValue( (coord, dir), out var wall ) )
        {
            wall.Delete();
            _walls.Remove( (coord, dir) );
        }

        CurrentMaze.WriteNetworkData();
    }
    
    private void ResetPlayers()
    {
        RestartCountdown = float.PositiveInfinity;
        NextLevelCountdown = float.PositiveInfinity;

        foreach ( var player in Players.ToArray() )
        {
            player.FirstSeenLevelIndex = LevelIndex;

            RespawnPlayer( player );
        }
    }
}
