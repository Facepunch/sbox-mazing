﻿using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mazing.Enemies;
using Mazing.UI;

//
// You don't need to put things in a namespace, but it doesn't hurt.
//
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

	[Net]
	public int LevelIndex { get; set; }

    [Net] public TimeSince RestartCountdown { get; set; }

    [Net] public TimeSince NextLevelCountdown { get; set; }

    public bool IsTransitioning => RestartCountdown > -1f && RestartCountdown < 0f ||
                                   NextLevelCountdown > -1f && NextLevelCountdown < 0f;

    private readonly List<ModelEntity> _mazeEntities = new List<ModelEntity>();
    private GridCoord[] _playerSpawns;

	public MazingGame()
	{
        if ( IsClient )
        {
            new HudRoot();
        }

        RestartCountdown = float.PositiveInfinity;
        NextLevelCountdown = float.PositiveInfinity;
    }

	[ConCmd.Admin("maze_generate")]
	public static void GenerateNewMaze()
	{
		MazingGame.Current.GenerateMaze();
	}

	public void GenerateMaze()
	{
		Host.AssertServer();

		foreach ( var entity in _mazeEntities )
		{
			entity.Delete();
		}

		_mazeEntities.Clear();

        var seed = Rand.Int(1, int.MaxValue - 1);

		Log.Info( $"Generating maze with seed {seed:x8} ");

        var typesToSpawn = TypeLibrary.GetTypes<Enemy>()
            .SelectMany(x =>
                TypeLibrary.GetDescription(x).GetAttributes<EnemySpawnAttribute>()
                    .Where(y => y.ShouldSpawn(LevelIndex))
                    .Select(y => x))
            .ToArray();

        foreach (var type in typesToSpawn)
        {
            TypeLibrary.Create<Enemy>(type);
        }

        var enemies = Entity.All.OfType<Enemy>().ToArray();
        var generated = MazeGenerator.Generate(seed, 8 + (LevelIndex / 4) * 4, MaxPlayers, enemies.Length, LevelIndex * 4 + 4);

        CurrentMaze = generated.MazeData;
        CurrentMaze.WriteNetworkData();

        _playerSpawns = generated.Players;

        ExitCell = generated.Exit;

        const float outerWallHeight = 128;
		const float innerWallHeight = 96f;
        const float wallModelHeight = 256f;
        const float borderHeight = outerWallHeight - 16f;

        var hatch = new Hatch();

		_mazeEntities.Add( hatch );

        for ( var i = 0; i < enemies.Length; i++ )
        {
            var enemyCell = generated.Enemies[i];

            enemies[i].Position = CellToPosition(enemyCell.Row + 0.5f, enemyCell.Col + 0.5f) + Vector3.Up * (2048f + 256f * i);
        }

        foreach ( var coinCell in generated.Coins )
        {
            _mazeEntities.Add( new Coin
            {
                Position = CellToPosition( coinCell.Row + 0.5f, coinCell.Col + 0.5f )
            } );
        }

        var key = new Key
        {
            Position = CellToPosition( generated.Key.Row + 0.5f, generated.Key.Col + 0.5f ) + Vector3.Up * 64f
        };

		_mazeEntities.Add( key );

		for (var row = 0; row <= CurrentMaze.Rows; row++)
		{
			for (var col = 0; col <= CurrentMaze.Cols; col++)
			{
				if (row < CurrentMaze.Rows && CurrentMaze.GetWall((row, col), Direction.West))
                {
                    var height = col <= 0 || col >= CurrentMaze.Cols ? outerWallHeight : innerWallHeight;

					_mazeEntities.Add(new Wall
					{
						Position = CellToPosition(row + 1f, col) + Vector3.Up * (height - wallModelHeight)
					});
				}

				if (col < CurrentMaze.Cols && CurrentMaze.GetWall((row, col), Direction.North))
				{
                    var height = row <= 0 || row >= CurrentMaze.Rows ? outerWallHeight : innerWallHeight;

					_mazeEntities.Add(new Wall
					{
						Position = CellToPosition(row, col) + Vector3.Up * (height - wallModelHeight),
						Rotation = Rotation.FromYaw(90f)
					});
				}

				var north = CurrentMaze.GetWall((row - 1, col), Direction.West);
				var south = CurrentMaze.GetWall((row, col), Direction.West);

				var west = CurrentMaze.GetWall((row, col - 1), Direction.North);
				var east = CurrentMaze.GetWall((row, col), Direction.North);

				if (north != south || west != east || north && west)
				{
                    var height = row <= 0 || row >= CurrentMaze.Rows || col <= 0 || col >= CurrentMaze.Cols
                        ? outerWallHeight : innerWallHeight;

					_mazeEntities.Add(new Post
					{
						Position = CellToPosition(row, col) + Vector3.Up * (height - wallModelHeight)
					});
				}
			}
		}

        _mazeEntities.Add( new Border
        {
            Position = Vector3.Up * borderHeight + CellToPosition( CurrentMaze.Rows, 0f )
        } );

        _mazeEntities.Add( new Border
        {
            Position = Vector3.Up * borderHeight + CellToPosition( 0f, CurrentMaze.Cols ),
            Rotation = Rotation.FromYaw( 180f )
        } );
    }

	public Vector3 CellToPosition( float row, float col ) => new Vector3( (col - ExitCell.Col - 0.5f) * 48f, (row - ExitCell.Row - 0.5f) * 48f, 0f );
    public Vector3 CellToPosition( GridCoord coord ) => new Vector3((coord.Col - ExitCell.Col - 0.5f) * 48f, (coord.Row - ExitCell.Row - 0.5f) * 48f, 0f);

    public Vector3 CellCenterToPosition(GridCoord coord) => CellToPosition(coord) + new Vector3(24f, 24f, 0f);

    public Vector3 GetCellCenter( Vector3 position )
    {
        var (row, col) = PositionToCell( position );

        return CellToPosition( row.FloorToInt() + 0.5f, col.FloorToInt() + 0.5f );
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

        return Entity.All.OfType<MazingPlayer>()
            .Any( x => x.IsAliveInMaze && x.GetCellIndex() == coord );
    }

    public bool IsEnemyInCell(GridCoord coord)
    {
        // TODO: optimize
        return Entity.All.OfType<Enemy>()
            .Any(x => x.GetCellIndex() == coord);
    }

    public GridCoord GetRandomCell()
    {
        return new GridCoord(Rand.Int(0, CurrentMaze.Rows - 1), Rand.Int(0, CurrentMaze.Cols - 1));
    }

    public bool IsInMaze(GridCoord cell)
    {
        if (cell.Col < 0 || cell.Row < 0 || cell.Col >= CurrentMaze.Cols || cell.Row >= CurrentMaze.Rows)
            return false;

        return true;
    }

    /// <summary>
    /// A client has joined the server. Make them a pawn to play with
    /// </summary>
    public override void ClientJoined( Client client )
	{
		base.ClientJoined( client );

		if ( CurrentMaze == null )
		{
			GenerateMaze();
		}

		// Create a pawn for this client to play with
		var mazingPlayer = new MazingPlayer( client );
		client.Pawn = mazingPlayer;

        RespawnPlayer( mazingPlayer );
    }

    private void RespawnPlayer( MazingPlayer player )
    {
        player.HasExited = false;

        var index = Array.IndexOf( Entity.All.OfType<MazingPlayer>().ToArray(), player );

        // Spawn in a random grid cell
        var spawnCell = _playerSpawns[index % _playerSpawns.Length];

        player.Position = CellToPosition(spawnCell.Row + 0.5f, spawnCell.Col + 0.5f) + Vector3.Up * 1024f;
        player.Respawn();
	}

	[Event.Tick.Server]
    public void ServerTick()
    {
        if ( !float.IsPositiveInfinity( RestartCountdown ) )
        {
            if ( RestartCountdown > 0f )
            {
                RestartCountdown = float.PositiveInfinity;

                LevelIndex = 0;

                ClearEnemies();
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

                ++LevelIndex;

                GenerateMaze();
                ResetPlayers();
            }

            return;
        }

        var allExited = true;
        var anyPlayers = false;
        var anyDeadPlayers = false;

        foreach ( var player in Entity.All.OfType<MazingPlayer>() )
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
        }
        else if ( !anyPlayers && anyDeadPlayers )
        {
            RestartCountdown = -3f;
        }
    }

    private void ClearEnemies()
    {
        var enemies = Entity.All.OfType<Enemy>().ToArray();

        foreach ( var enemy in enemies )
        {
            enemy.Delete();
        }
    }

    private void ResetPlayers()
    {
        RestartCountdown = float.PositiveInfinity;
        NextLevelCountdown = float.PositiveInfinity;

        foreach ( var player in Entity.All.OfType<MazingPlayer>().ToArray() )
        {
            RespawnPlayer( player );
        }
    }
}
