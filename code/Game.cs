using Sandbox;
using Sandbox.UI.Construct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
	public new static MazingGame Current => Game.Current as MazingGame;

	private MazeData _currentMaze;
    private (int Row, int Col) _exitCell;
	private readonly List<ModelEntity> _mazeGeometry = new List<ModelEntity>();

	public MazingGame()
	{
	}

	[ConCmd.Admin("maze_generate")]
	public static void GenerateNewMaze()
	{
		MazingGame.Current.GenerateMaze();
	}

	public void GenerateMaze()
	{
		Host.AssertServer();

		foreach ( var entity in _mazeGeometry )
		{
			entity.Delete();
		}

		_mazeGeometry.Clear();

        var seed = Rand.Int(1, int.MaxValue - 1);

		Log.Info( $"Generating maze with seed {seed:x8} ");

		_currentMaze = MazeGenerator.Generate(seed);
		_currentMaze.Print();

        _exitCell = (Rand.Int(_currentMaze.Rows - 1), Rand.Int(_currentMaze.Cols - 1));

        const float outerWallHeight = 128;
		const float innerWallHeight = 96f;
        const float wallModelHeight = 256f;

		for (var row = 0; row <= _currentMaze.Rows; row++)
		{
			for (var col = 0; col <= _currentMaze.Cols; col++)
			{
				if (row < _currentMaze.Rows && _currentMaze.GetWall(row, col, Direction.West))
                {
                    var height = col <= 0 || col >= _currentMaze.Cols ? outerWallHeight : innerWallHeight;

					_mazeGeometry.Add(new Wall
					{
						Position = CellToPosition(row + 1f, col) + Vector3.Up * (height - wallModelHeight)
					});
				}

				if (col < _currentMaze.Cols && _currentMaze.GetWall(row, col, Direction.North))
				{
                    var height = row <= 0 || row >= _currentMaze.Rows ? outerWallHeight : innerWallHeight;

					_mazeGeometry.Add(new Wall
					{
						Position = CellToPosition(row, col) + Vector3.Up * (height - wallModelHeight),
						Rotation = Rotation.FromYaw(90f)
					});
				}

				var north = _currentMaze.GetWall(row - 1, col, Direction.West);
				var south = _currentMaze.GetWall(row, col, Direction.West);

				var west = _currentMaze.GetWall(row, col - 1, Direction.North);
				var east = _currentMaze.GetWall(row, col, Direction.North);

				if (north != south || west != east || north && west)
				{
                    var height = row <= 0 || row >= _currentMaze.Rows || col <= 0 || col >= _currentMaze.Cols
                        ? outerWallHeight : innerWallHeight;

					_mazeGeometry.Add(new Post
					{
						Position = CellToPosition(row, col) + Vector3.Up * (height - wallModelHeight)
					});
				}
			}
		}
	}

	private Vector3 CellToPosition( float row, float col ) => new Vector3( (col - _exitCell.Col - 0.5f) * 48f, (row - _exitCell.Row - 0.5f) * 48f, 0f );

	/// <summary>
	/// A client has joined the server. Make them a pawn to play with
	/// </summary>
	public override void ClientJoined( Client client )
	{
		base.ClientJoined( client );

		if ( _currentMaze == null )
		{
			GenerateMaze();
		}

		// Create a pawn for this client to play with
		var mazingPlayer = new MazingPlayer();
		client.Pawn = mazingPlayer;

		// Spawn in a random grid cell
		var spawnCell = (row: Rand.Int( 0, _currentMaze.Rows - 1 ), col: Rand.Int( 0, _currentMaze.Cols - 1 ));

		mazingPlayer.Respawn();
		mazingPlayer.Position = CellToPosition( spawnCell.row + 0.5f, spawnCell.col + 0.5f ) + Vector3.Up * 32f;
	}
}
