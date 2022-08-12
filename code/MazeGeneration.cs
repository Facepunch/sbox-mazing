using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing;
using Sandbox;

namespace Mazing;

public record struct GeneratedMaze( MazeData MazeData,
	GridCoord Exit,
    GridCoord Key,
	GridCoord[] Players,
    GridCoord[] Enemies,
    GridCoord[] Coins );

public static class MazeGenerator
{
    public static GeneratedMaze GenerateLobby()
    {
		var (maze, specialCells) = MazeData.Load( 8, 7, FileSystem.Mounted.OpenRead( "mazes/lobby.txt" ) );

        var players = specialCells
            .Where( x => x.Char == 'P' )
            .Select( x => x.Coord )
            .ToArray();

        var enemies = specialCells
            .Where(x => x.Char == 'W')
            .Select(x => x.Coord)
            .ToArray();

        var exits = specialCells
            .Where(x => x.Char == 'E')
            .Select(x => x.Coord)
            .ToArray();

        var coins = specialCells
            .Where(x => x.Char == 'C')
            .Select(x => x.Coord)
            .ToArray();

        var keys = specialCells
            .Where(x => x.Char == 'K')
            .Select(x => x.Coord)
            .ToArray();

        return new GeneratedMaze( maze,
            exits[Rand.Int( exits.Length - 1 )],
            keys[Rand.Int( keys.Length - 1 )],
            players,
            new[] { enemies[Rand.Int( enemies.Length - 1 )] },
            coins );
    }

	public static GeneratedMaze Generate( int seed, int rows, int cols, int playerCount, int enemyCount, int coinCount )
	{
        if ( playerCount + enemyCount + coinCount + 2 > rows * cols)
        {
            throw new ArgumentException( "Maze is too small to fit that many players, enemies, coins, an exit and a key." );
        }

		var rand = new Random( seed );

		const int stride = 4;

        if ( rows <= 0 || rows % stride != 0 || cols <= 0 || cols % stride != 0 )
        {
            throw new ArgumentException( $"Size must be a multiple of {stride}." );
        }

		var parts = MazeData.LoadAll().ToList();
		var maze = new MazeData( rows, cols );

		var bigParts = parts
			.Where( x => x.Rows >= 8 || x.Cols >= 8 )
			.ToArray();

		var smallParts = parts
			.Except( bigParts )
			.ToArray();

		bigParts = new[] { bigParts[rand.Next( bigParts.Length )] };
		smallParts = Enumerable.Range( 0, 3 )
			.Select( _ => smallParts[rand.Next( smallParts.Length )] )
			.ToArray();

		var occupied = new bool[rows, cols];
		var totalCoverage = 0;

		while ( totalCoverage < rows * cols / 2 )
		{
			var placed = PlaceRandom( rand, maze, bigParts, occupied, stride );
			if ( placed == null ) break;
			totalCoverage += placed.Rows * placed.Cols;
		}

		while ( totalCoverage < rows * cols)
		{
			var placed = PlaceRandom( rand, maze, smallParts, occupied, stride );
			if ( placed == null ) break;
			totalCoverage += placed.Rows * placed.Cols;
		}

        var unvisited = Enumerable.Range( 0, rows )
            .SelectMany( x => Enumerable.Range( 0, cols ).Select( y => (row: x, col: y) ) )
            .ToHashSet();

		var islands = new List<HashSet<GridCoord>>();
		var queue = new Queue<GridCoord>();

		while ( unvisited.Count > 0 )
		{
			var root = unvisited.First();
			unvisited.Remove( root );

			var island = new HashSet<GridCoord> { root };

			islands.Add( island );

			queue.Enqueue( root );

			while ( queue.Count > 0 )
			{
				var next = queue.Dequeue();

				foreach ( var (dir, delta) in MazeData.Directions )
				{
					if ( maze.GetWall( next, dir ) )
					{
						continue;
					}

					var neighbor = next + delta;

					if ( island.Add( neighbor ) && unvisited.Remove( neighbor ) )
					{
						queue.Enqueue( neighbor );
					}
				}
			}
		}

        var players = new List<GridCoord>();
		var enemies = new List<GridCoord>();
        var coins = new List<GridCoord>();

        var available = new Queue<GridCoord>( Enumerable.Range( 0, rows )
            .SelectMany( row => Enumerable.Range( 0, cols ).Select( col => new GridCoord( row, col ) ) )
            .OrderBy( x => rand.NextSingle() ) );

        var exit = available.Dequeue();
        var key = available.Dequeue();

        for ( var i = 0; i < playerCount; ++i )
        {
            players.Add( available.Dequeue() );
        }

        for ( var i = 0; i < enemyCount; ++i )
        {
            enemies.Add( available.Dequeue() );
        }

        for ( var i = 0; i < coinCount; ++i )
        {
            coins.Add( available.Dequeue() );
        }

		var possibleBridges = new List<(GridCoord From, Direction Dir, GridCoord To)>();

		while ( islands.Count > 1 )
		{
			islands.Sort( ( a, b ) => b.Count - a.Count );

			var smallest = islands[^1];
			islands.RemoveAt( islands.Count - 1 );

			possibleBridges.Clear();

			foreach ( var coord in smallest )
			{
				foreach ( var (dir, delta) in MazeData.Directions )
				{
					if ( !maze.GetWall(coord, dir ) )
					{
						continue;
					}

					var neighbor = coord + delta;

					if ( neighbor.Row < 0 || neighbor.Row >= rows || neighbor.Col < 0 || neighbor.Col >= cols )
					{
						continue;
					}

					if ( smallest.Contains( neighbor ) )
					{
						continue;
					}

					possibleBridges.Add( (coord, dir, neighbor) );
				}
			}

			if ( possibleBridges.Count == 0 )
			{
				Log.Warning( "No possible bridges?" );
				continue;
			}

			var bridge = possibleBridges[rand.Next( possibleBridges.Count )];

			maze.SetWall( bridge.From, bridge.Dir, false );

			var neighborIsland = islands.First( x => x.Contains( bridge.To ) );

			foreach ( var item in smallest )
			{
				neighborIsland.Add( item );
			}
		}

        var size = MathF.Sqrt( rows * cols ).FloorToInt();

		var extraConnectivityCount = rand.Next( size / 2, size );
		var allWalls = new List<(GridCoord From, Direction dir, GridCoord To)>();

		for ( var row = 0; row < rows; row++ )
		{
			for ( var col = 0; col < cols; col++ )
			{
				if ( col > 0 && maze.GetWall( (row, col), Direction.West ) )
				{
					allWalls.Add( ((row, col), Direction.West, (row, col - 1)) );
				}

				if ( row > 0 && maze.GetWall( (row, col), Direction.North ) )
				{
					allWalls.Add( ((row, col), Direction.North, (row - 1, col)) );
				}
			}
		}

		for ( var i = 0; i < allWalls.Count; i++ )
		{
			var swapIndex = rand.Next( i, allWalls.Count );

			(allWalls[i], allWalls[swapIndex]) = (allWalls[swapIndex], allWalls[i]);
		}

		var minCycleLength = 12;

		while ( extraConnectivityCount > 0 && allWalls.Count > 0 )
		{
			var next = allWalls[^1];
			allWalls.RemoveAt( allWalls.Count - 1 );

			var dist = maze.GetDistance( next.From, next.To );

			if ( dist < minCycleLength )
			{
				continue;
			}

			maze.SetWall( next.From, next.dir, false );
		}

        return new GeneratedMaze( maze, exit, key, players.ToArray(), enemies.ToArray(), coins.ToArray() );
    }

	private static MazeData PlaceRandom( Random rand, MazeData maze, IEnumerable<MazeData> available, bool[,] occupied, int stride )
	{
		var allPlacements = available
			.GroupBy( x => (rows: x.Rows, cols: x.Cols) )
			.Select( x => (
				placements: GetAllPlacementsWithTranspose( x.Key.rows, x.Key.cols, occupied, stride ).ToArray(),
				data: x.ToArray()) )
			.Where( x => x.placements.Length > 0 )
			.SelectMany( x => x.data.Select( y => (data: y, placements: x.placements) ) )
			.ToArray();

		if ( allPlacements.Length == 0 )
		{
			return null;
		}

		var (data, placements) = allPlacements[rand.Next( allPlacements.Length )];
		var placement = placements[rand.Next( placements.Length )];

		var transform = (rand.NextDouble() < 0.5 ? MazeTransform.FlipX : 0)
						| (rand.NextDouble() < 0.5 ? MazeTransform.FlipY : 0)
						| (placement.transpose ? MazeTransform.Transpose : 0);

		MazeData.Copy( data, 0, 0, transform, maze, placement.row, placement.col, data.Rows, data.Cols );

		var (rows, cols) = placement.transpose
			? (data.Cols, data.Rows)
			: (data.Rows, data.Cols);

		for ( var row = 0; row < rows; row++ )
		{
			for ( var col = 0; col < cols; col++ )
			{
				occupied[placement.row + row, placement.col + col] = true;
			}
		}

		return data;
	}

	private static IEnumerable<(int row, int col, bool transpose)> GetAllPlacementsWithTranspose(
		int rows, int cols, bool[,] occupied, int stride )
	{
		return GetAllPlacements( rows, cols, occupied, stride )
			.Select( x => (x.row, x.col, false) )
			.Concat( GetAllPlacements( cols, rows, occupied, stride )
				.Select( x => (x.row, x.col, true) ) );
	}

	private static IEnumerable<(int row, int col)> GetAllPlacements(
		int rows, int cols, bool[,] occupied, int stride )
	{
		for ( var row = 0; row <= occupied.GetLength( 0 ) - rows; row += stride )
		{
			for ( var col = 0; col <= occupied.GetLength( 1 ) - cols; col += stride )
			{
				var blocked = false;

				for ( var r = 0; r < rows && !blocked; r += stride )
				{
					for ( var c = 0; c < cols; c += stride )
					{
						if ( occupied[row + r, col + c] )
						{
							blocked = true;
							break;
						}
					}
				}

				if ( !blocked )
				{
					yield return (row, col);
				}
			}
		}
	}

}
