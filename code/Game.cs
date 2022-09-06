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
    public const int TotalLevelCount = 50;

    public const int MaxPlayers = 8;

    [ConVar.Replicated("mazing_daily", Help = "Enable to do a daily challenge.")]
    public static bool StartDailyChallenge { get; set; }

    [Net, HideInEditor]
    public bool DailyChallengeEnabled { get; set; }

    [Net, HideInEditor]
    public bool DailyChallengeComplete { get; set; }

    [Net, HideInEditor]
    public DateTime DailyChallengeDateUtc { get; set; }

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
    public Lava Lava { get; set; }

    [Net]
    public bool IsEditorMode { get; set; }

    private bool _hasCheated;

    private int _nextLevelSeed = -1;

    private readonly HashSet<Entity> _worldEntities = new();
    private bool _firstSpawn = true;

    private readonly List<Enemy> _enemies = new();
    private readonly List<(TypeDescription Type, GridCoord Coord)> _enemiesToSpawn = new();

    private readonly Dictionary<TypeDescription, int> _enemyTypeLastSpawnLevel
        = new Dictionary<TypeDescription, int>();

    public IEnumerable<MazingPlayer> Players => Client.All
        .Select( x => x.Pawn )
        .OfType<MazingPlayer>();

    public IEnumerable<MazingPlayer> PlayersAliveInMaze => Players
        .Where( x => x.IsAliveInMaze );

    public IEnumerable<Enemy> Enemies => _enemies.Where(x => x.IsValid());

    public IEnumerable<Treasure> Treasure => Entity.All.OfType<Treasure>();

    public bool IsTransitioning => RestartCountdown > -1f && RestartCountdown < 0f ||
                                   NextLevelCountdown > -1f && NextLevelCountdown < 0f;

    private readonly Dictionary<(GridCoord Coord, Direction Dir), ModelEntity> _walls = new();
    private GridCoord[] _playerSpawns;

    private readonly List<TimeSpan> _levelTimes = new List<TimeSpan>();

    [Net]
    public bool LevelStarted { get; set; }

    [Net]
    public bool LevelCompleted { get; set; }

    [Net]
    public TimeSince SinceLevelStart { get; set; }

    [Net]
    public TimeSpan LastLevelTime { get; set; }

    [Net]
    public TimeSpan LastTotalTime { get; set; }

    public TimeSpan LevelTime => !LevelStarted
        ? TimeSpan.Zero : !LevelCompleted
            ? TimeSpan.FromSeconds(SinceLevelStart)
            : LastLevelTime;

    public TimeSpan TotalTime => LastTotalTime + LevelTime;

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
        _enemiesToSpawn.Clear();

        if ( seed == -1 )
        {
            seed = Rand.Int(1, int.MaxValue - 1);
        }

        var rand = new Random(seed);

		Log.Info( $"Generating level {LevelIndex + 1} with seed {seed:x8} ");
        
        var (rows, cols) = GetLevelSize( LevelIndex, rand.Next() );
        
        var generated = LevelIndex == 0
            ? MazeGenerator.GenerateLobby()
            : LevelIndex == TotalLevelCount - 1
                ? MazeGenerator.GenerateFinalLevel(rand.Next(), MaxPlayers )
                : MazeGenerator.Generate(rand.Next(), rows, cols, MaxPlayers,
                    MazeGenerator.GetSpawningEnemyCounts(LevelIndex, rand.Next(), _enemyTypeLastSpawnLevel),
                    MazeGenerator.GetSpawningTreasureCounts((LevelIndex * 2 + 2) * Items.Treasure.GetValue(TreasureKind.Emerald), rand.Next()));

        CurrentMaze = generated.MazeData;
        CurrentMaze.WriteNetworkData();

        if (LevelIndex <= 0)
        {
            _levelTimes.Clear();
        }

        SinceLevelStart = -1.6f;
        LastLevelTime = TimeSpan.Zero;
        LastTotalTime = TimeSpan.FromSeconds(_levelTimes.Sum(x => x.TotalSeconds));

        LevelStarted = false;
        LevelCompleted = false;

        _playerSpawns = generated.Players;

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

        _enemiesToSpawn.AddRange(generated.Enemies);

        TotalTreasureValue = 0;

        foreach (var (treasureKind, cell) in generated.Treasure)
        {
            var treasure = new Treasure(treasureKind)
            {
                Position = CellCenterToPosition(cell)
            };

            TotalTreasureValue += treasure.Value;
        }
        
        Key = new Key
        {
            Position = CellCenterToPosition( generated.Key ) + Vector3.Up * 64f
        };

        if (LevelIndex == TotalLevelCount - 1)
        {
            Lava = new Lava
            {
                Position = CellToPosition(0f, CurrentMaze.Cols * 0.5f)
            };

            var diamond = new BigTreasure
            {
                Position = CellToPosition(6f, 4f)
            };
        }
        else
        {
            Lava = null;
        }

		for (var row = 0; row <= CurrentMaze.Rows; row++)
		{
			for (var col = 0; col <= CurrentMaze.Cols; col++)
			{
				if (row < CurrentMaze.Rows && CurrentMaze.GetWall((row, col), Direction.West))
                {
                    var isOuter = col <= 0 || col >= CurrentMaze.Cols;
                    var height = isOuter ? outerWallHeight : innerWallHeight;
                    var wall = new Wall
                    {
                        Position = CellToPosition( row + 1f, col ) + Vector3.Up * (height - wallModelHeight)
                    };

                    _walls.Add(((row, col), Direction.West), wall);

                }

				if (col < CurrentMaze.Cols && CurrentMaze.GetWall((row, col), Direction.South))
                {
                    var isOuter = row <= 0 || row >= CurrentMaze.Rows;
                    var height = isOuter ? outerWallHeight : innerWallHeight;
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
                    var isOuter = row <= 0 || row >= CurrentMaze.Rows || col <= 0 || col >= CurrentMaze.Cols;
                    var height = isOuter ? outerWallHeight : innerWallHeight;

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

    public bool IsCellEmpty(GridCoord coord)
    {
        return !IsEnemyInCell(coord)
            && !IsPlayerInCell(coord)
            && !(Key != null && Key.GetCellIndex() == coord)
            && ExitCell != coord;
    }

    public bool IsEnemyInCell(GridCoord coord)
    {
        // TODO: optimize
        return Enemies
            .Any( x => x.GetCellIndex() == coord );
    }

    [ThreadStatic]
    private static HashSet<GridCoord> _sFloodFillSet;

    [ThreadStatic]
    private static List<GridCoord> _sFloodFillList;

    public GridCoord GetRandomConnectedEmptyCell(GridCoord connectedCell)
    {
        var set = _sFloodFillSet ??= new HashSet<GridCoord>();
        var list = _sFloodFillList ?? new List<GridCoord>();

        set.Clear();
        list.Clear();

        set.Add(connectedCell);
        list.Add(connectedCell);

        for (var i = 0; i < list.Count; ++i)
        {
            var prev = list[i];

            foreach (var (dir, delta) in MazeData.Directions)
            {
                var next = prev + delta;

                if (set.Contains(next) || !IsInMaze(next) || CurrentMaze.GetWall(prev, dir))
                {
                    continue;
                }

                set.Add(next);
                list.Add(next);
            }
        }

        while (list.Count > 0)
        {
            var index = Rand.Int(0, list.Count - 1);
            var cell = list[index];

            list.RemoveAt(index);

            if (IsCellEmpty(cell))
            {
                return cell;
            }
        }

        return connectedCell;
    }

    public bool IsInMaze(GridCoord cell)
    {
        return CurrentMaze?.Contains(cell) ?? false;
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

            DailyChallengeEnabled = StartDailyChallenge;
            DailyChallengeDateUtc = DateTime.UtcNow;

            StartDailyChallenge = false;
        }

        _ = ClientJoinedAsync(client);
    }

    private async Task ClientJoinedAsync( Client client )
    {
        var isSpectator = DailyChallengeComplete;

        if (DailyChallengeEnabled && !DailyChallengeComplete)
        {
            var result = await GameServices.Leaderboard.Query(Global.GameIdent,
                client.PlayerId, GetDailyChallengeBucket(DailyChallengeDateUtc, "money"));

            Log.Info($"Found {result.Count} entries for {client.Name}");

            foreach (var entry in result.Entries)
            {
                Log.Info($"{entry.DisplayName}: {entry.Rating}");
            }

            if (result.Count > 0)
            {
                isSpectator = true;

                if (client.IsListenServerHost)
                {
                    DailyChallengeComplete = true;
                }
            }
        }

        // Create a pawn for this client to play with
        var mazingPlayer = new MazingPlayer(client)
        {
            IsSpectatorOnly = isSpectator,
            FirstSeenLevelIndex = LevelIndex
        };

        client.Pawn = mazingPlayer;

        RespawnPlayer(mazingPlayer);

        if (LevelIndex > 0 || mazingPlayer.IsSpectatorOnly)
        {
            mazingPlayer.Kill(Vector3.Up, "{0} has joined as a ghost", this, false);
        }
        else
        {
            ClientJoinNotify(client.Name, client.PlayerId);
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

    private void SpawnEnemies()
    {
        if (_enemiesToSpawn.Count == 0) return;

        var playerBoundsMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity);
        var playerBoundsMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity);

        var anyPlayers = false;

        foreach (var player in PlayersAliveInMaze)
        {
            anyPlayers = true;

            playerBoundsMin = Vector3.Min(playerBoundsMin, player.Position);
            playerBoundsMax = Vector3.Max(playerBoundsMax, player.Position);
        }

        if (!anyPlayers)
        {
            return;
        }

        const float spawnDist = 48f * 8;

        for (var i = _enemiesToSpawn.Count - 1; i >= 0; --i)
        {
            var (enemyType, cell) = _enemiesToSpawn[i];
            var spawnPos = CellCenterToPosition(cell);

            var distX = Math.Max(spawnPos.x - playerBoundsMax.x, playerBoundsMin.x - spawnPos.x);
            var distY = Math.Max(spawnPos.y - playerBoundsMax.y, playerBoundsMin.y - spawnPos.y);

            var dist = Math.Max(distX, distY);

            if (dist >= spawnDist) continue;

            _enemiesToSpawn.RemoveAt(i);

            var enemy = enemyType.Create<Enemy>();

            enemy.PostSpawn(_enemies.Count, spawnPos);

            _enemies.Add(enemy);
        }
    }

    private static string GetDailyChallengeBucket(DateTime dateUtc, string category)
    {
        var day = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day);

        return $"daily-{day.Year}-{day.Month}-{day.Day}-{category}";
    }

    private static int GetDailyChallengeSeed(DateTime dateUtc, int levelIndex)
    {
        var day = new DateTime(dateUtc.Year, dateUtc.Month, dateUtc.Day);
        var firstDay = new DateTime(2022, 9, 6);
        var diff = (int) Math.Round((day - firstDay).TotalDays);

        var rand = new Random(diff ^ 0x7dcb0789);

        for (var i = 0; i < levelIndex + 10; ++i)
        {
            rand.Next();
        }

        return rand.Next();
    }

    [Event.Tick.Server]
    public void ServerTick()
    {
        SpawnEnemies();

        for (var i = _enemies.Count - 1; i >= 0; --i)
        {
            var enemy = _enemies[i];

            if (!enemy.IsValid())
            {
                _enemies.RemoveAt(i);
                continue;
            }

            enemy.ServerTick();
        }

        foreach ( var treasure in Treasure )
        {
            treasure.ServerTick();
        }

        Lava?.ServerTick();

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

                GenerateMaze( DailyChallengeEnabled
                    ? GetDailyChallengeSeed(DailyChallengeDateUtc, LevelIndex)
                    : _nextLevelSeed );
                ResetPlayers();

                _nextLevelSeed = -1;
            }

            return;
        }

        var anySpawning = false;
        var anyExited = false;
        var allExited = true;
        var anyPlayers = false;
        var anyDeadPlayers = false;

        foreach ( var player in Players )
        {
            if (player.IsSpectatorOnly) continue;

            if ( !player.IsAlive )
            {
                anyDeadPlayers = true;
                continue;
            }

            anyPlayers = true;

            if (player.IsSpawning)
            {
                anySpawning = true;
            }

            if (player.HasExited)
            {
                anyExited = true;
                continue;
            }

            allExited = false;
			break;
        }

        if (!LevelStarted && !anySpawning)
        {
            LevelStarted = true;

            SinceLevelStart = 0f;
        }

        if (LevelStarted && !LevelCompleted && anyExited)
        {
            LevelCompleted = true;
            LastLevelTime = TimeSpan.FromSeconds(SinceLevelStart);

            if (LevelIndex > 0)
            {
                _levelTimes.Add(LastLevelTime);
            }
        }

        if ( anyPlayers && allExited )
        {
            if (!_hasCheated && !Host.IsToolsEnabled)
            {
                foreach (var player in All.OfType<MazingPlayer>())
                {
                    if (player.Client == null) continue;
                    if (LevelIndex < player.FirstSeenLevelIndex * 2 || player.IsSpectatorOnly)
                    {
                        Log.Info($"Not submitting score for {player.Client.Name}");
                    }

                    GameServices.UpdateLeaderboard(player.Client.PlayerId, TotalCoins, "money");
                    GameServices.UpdateLeaderboard(player.Client.PlayerId, LevelIndex + 1, "depth");

                    if (DailyChallengeEnabled)
                    {
                        GameServices.UpdateLeaderboard(player.Client.PlayerId, TotalCoins, GetDailyChallengeBucket(DailyChallengeDateUtc, "money"));
                        GameServices.UpdateLeaderboard(player.Client.PlayerId, LevelIndex + 1, GetDailyChallengeBucket(DailyChallengeDateUtc, "depth"));
                    }

                    if (LevelIndex == TotalLevelCount - 1)
                    {
                        GameServices.UpdateLeaderboard(player.Client.PlayerId, (float) TotalTime.TotalSeconds, "time50");

                        if (DailyChallengeEnabled)
                        {
                            GameServices.UpdateLeaderboard(player.Client.PlayerId, (float)TotalTime.TotalSeconds, GetDailyChallengeBucket(DailyChallengeDateUtc, "time50"));
                        }
                    }
                }
            }

            if (LevelIndex >= TotalLevelCount - 1)
            {
                if (DailyChallengeEnabled)
                {
                    DailyChallengeComplete = true;
                }

                RestartCountdown = -5f;
                ClientNotifyFinalScore( true, TotalCoins );
            }
            else
            {
                NextLevelCountdown = -1.5f;
                NextLevelIndex = LevelIndex + 1;
            }
        }
        else if ( !anyPlayers && anyDeadPlayers )
        {
            if (DailyChallengeEnabled)
            {
                DailyChallengeComplete = true;
            }

            RestartCountdown = -3f;
            ClientNotifyFinalScore( false, TotalCoins );
        }
    }
    
    [ClientRpc]
    public void ClientNotifyFinalScore( bool victory, int score )
    {
        if (victory)
        {
            ChatBox.AddInformation($"You have escaped! Final score: ${score}");
        }
        else
        {
            ChatBox.AddInformation($"Everyone is dead! Final score: ${score}");
        }
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

            if (DailyChallengeComplete)
            {
                player.IsSpectatorOnly = true;
            }

            if (!player.IsSpectatorOnly)
            {
                RespawnPlayer(player);
            }
            else if (player.IsAlive)
            {
                player.Kill(Vector3.Zero, "{0} is now a spectator", null, false);
            }
        }
    }

    public static T GetRandomWeighted<T>(params (T Value, float Weight)[] weights)
    {
        var totalWeight = weights.Sum(x => x.Weight);
        var randomWeight = Rand.Float(0f, totalWeight);

        for (var i = 0; i < weights.Length; ++i)
        {
            randomWeight -= weights[i].Weight;

            if (randomWeight < 0f)
            {
                return weights[i].Value;
            }
        }

        return default;
    }
}
