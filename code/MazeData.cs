using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing;

public enum Direction
{
	North,
	East,
	South,
	West
}

[Flags]
public enum MazeTransform
{
	Identity = 0,
	FlipX = 1,
	FlipY = 2,
	Transpose = 4
}

public readonly struct GridCoord : IEquatable<GridCoord>
{
    public static bool operator ==( GridCoord a, GridCoord b )
    {
        return a.Row == b.Row && a.Col == b.Col;
    }

    public static bool operator !=( GridCoord a, GridCoord b )
    {
        return a.Row != b.Row || a.Col != b.Col;
	}

    public static implicit operator GridCoord( Direction dir )
    {
        return MazeData.Directions[(int)dir].Delta;
	}

	public static implicit operator GridCoord( (int Row, int Col) tuple )
    {
        return new GridCoord( tuple.Row, tuple.Col );
	}

    public static implicit operator (int Row, int Col)( GridCoord coord )
    {
        return (coord.Row, coord.Col);
    }

    public static GridCoord operator +( GridCoord a, GridCoord b )
    {
        return new GridCoord( a.Row + b.Row, a.Col + b.Col );
	}

    public static GridCoord operator -( GridCoord a, GridCoord b )
    {
        return new GridCoord(a.Row - b.Row, a.Col - b.Col);
    }

    public static GridCoord operator *( GridCoord a, int scale )
    {
        return new GridCoord( a.Row * scale, a.Col * scale );
    }

	public readonly int Row;
    public readonly int Col;

    public GridCoord( int row, int col )
    {
        Row = row;
        Col = col;
    }

    public bool Equals( GridCoord other )
    {
        return Row == other.Row && Col == other.Col;
    }

    public override bool Equals( object obj )
    {
        return obj is GridCoord other && Equals( other );
    }

    public override int GetHashCode()
    {
        return HashCode.Combine( Row, Col );
    }
}

public partial class MazeData : BaseNetworkable, INetworkSerializer
{
	public int Rows { get; private set; }
	public int Cols { get; private set; }

	private bool[] _vertWalls;
	private bool[] _horzWalls;

	private static Regex PartNameRegex = new Regex( @"^(?<cols>[0-9]+)x(?<rows>[0-9]+)_" );

	private static Regex HorzWallRegex = new Regex( @"^\+([- ]{3}\+)+$" );
	private static Regex VertWallRegex = new Regex( @"^[| ]([ ]{3}[| ])+$" );

	public static IEnumerable<MazeData> LoadAll()
	{
        foreach ( var file in FileSystem.Mounted.FindFile( "mazes", "*.txt", true ) )
        {
			var name = PartNameRegex.Match( file );

			if ( !name.Success )
			{
				continue;
			}

			var rows = int.Parse( name.Groups["rows"].Value );
			var cols = int.Parse( name.Groups["cols"].Value );

			if ( rows <= 0 || cols <= 0 || rows % 4 != 0 || cols % 4 != 0 || rows > 16 || cols > 16 )
			{
				Log.Warning( $"Maze data \"{file}\" is an invalid size. Expected a multiple of 4, up to 16." );
				continue;
			}

			yield return Load( rows, cols, FileSystem.Mounted.OpenRead( $"mazes/{file}" ) );
		}
	}

	public static MazeData Load( int rows, int cols, Stream stream )
	{
		using ( var reader = new StreamReader( stream ) )
		{
			return Load( rows, cols, reader );
		}
	}

	public static MazeData Load( int rows, int cols, TextReader reader )
	{
		var part = new MazeData( rows, cols );

		for ( var row = 0; row <= rows; ++row )
		{
			// Horizontal walls

			var line = reader.ReadLine();

			if ( line == null )
			{
				throw new EndOfStreamException();
			}

			var horzWallsMatch = HorzWallRegex.Match( line );

			if ( !horzWallsMatch.Success || line.Length != cols * 4 + 1 )
			{
				throw new Exception( $"Bad maze part format (horizontal walls on row {row})" );
			}

			for ( var col = 0; col < cols; ++col )
			{
				part.SetWall( (row, col), Direction.North, horzWallsMatch.Groups[1].Captures[col].Value.Contains( '-' ) );
			}

			if ( row >= rows )
			{
				break;
			}

			// Vertical walls

			line = reader.ReadLine();

			if ( line == null )
			{
				throw new EndOfStreamException();
			}

			var vertWallsMatch = VertWallRegex.Match( line );

			if ( !vertWallsMatch.Success || line.Length != cols * 4 + 1 )
			{
				throw new Exception( $"Bad maze part format (vertical walls on row {row})" );
			}

			for ( var col = 0; col <= cols; ++col )
			{
				part.SetWall( (row, col), Direction.West, line[col * 4] == '|' );
			}
		}

		return part;
	}

	public static void Copy( MazeData src, MazeTransform srcTransform,
		MazeData dst, int dstRow, int dstCol )
	{
		var transpose = (srcTransform & MazeTransform.Transpose) != 0;

		Copy( src, 0, 0, srcTransform, dst, 0, 0,
			transpose ? src.Cols : src.Rows, transpose ? src.Rows : src.Cols);
	}

	public static void Copy( MazeData src, int srcRow, int srcCol,
		MazeTransform srcTransform,
		MazeData dst, int dstRow, int dstCol, int rows, int cols )
	{
		var flipX = (srcTransform & MazeTransform.FlipX) != 0;
		var flipY = (srcTransform & MazeTransform.FlipY) != 0;
		var transpose = (srcTransform & MazeTransform.Transpose) != 0;

		for ( var row = 0; row <= rows; ++row )
		{
			for ( var col = 0; col <= cols; ++col )
			{
				var relRow = row;
				var relCol = col;
				var relWest = Direction.West;
				var relNorth = Direction.North;

				if ( flipX )
				{
					relCol = src.Cols - relCol - 1;
					relWest = Direction.East;
				}

				if ( flipY )
				{
					relRow = src.Rows - relRow - 1;
					relNorth = Direction.South;
				}

				if ( transpose )
				{
					(relRow, relCol) = (relCol, relRow);
					relWest = relWest == Direction.West ? Direction.North : Direction.South;
					relNorth = relNorth == Direction.North ? Direction.West : Direction.East;
				}

				if ( src.GetWall( (srcRow + row, srcCol + col), Direction.West ) )
				{
					dst.SetWall( (dstRow + relRow, dstCol + relCol), relWest, true );
				}

				if ( src.GetWall( (srcRow + row, srcCol + col), Direction.North ) )
				{
					dst.SetWall( (dstRow + relRow, dstCol + relCol), relNorth, true );
				}
			}
		}
	}

    public MazeData()
    {

    }

	public MazeData( int rows, int cols )
	{
		Rows = rows;
		Cols = cols;

		_vertWalls = new bool[(cols + 1) * rows];
		_horzWalls = new bool[(rows + 1) * cols];
	}

	public void SetBorder( bool isSolid )
	{
		for ( var i = 0; i < Cols; i++ )
		{
			SetWall( (0, i), Direction.North, isSolid );
			SetWall( (Rows - 1, i), Direction.South, isSolid );
		}

		for ( var i = 0; i < Rows; i++ )
		{
			SetWall( (i, 0), Direction.West, isSolid );
			SetWall( (i, Cols - 1), Direction.East, isSolid );
		}
	}

	private static void ForceNorthOrWest( ref GridCoord coord, ref Direction dir )
	{
		switch ( dir )
		{
			case Direction.South:
				dir = Direction.North;
                coord += (1, 0);
				break;
			case Direction.East:
				dir = Direction.West;
                coord += (0, 1);
				break;
		}
	}
	
	public bool GetWall( GridCoord coord, Direction dir )
	{
		ForceNorthOrWest( ref coord, ref dir );

		switch ( dir )
		{
			case Direction.North:
				if (coord.Row < 0 || coord.Row > Rows || coord.Col < 0 || coord.Col >= Cols ) return false;

				return _horzWalls[Cols * coord.Row + coord.Col];

			case Direction.West:
				if (coord.Col < 0 || coord.Col > Cols || coord.Row < 0 || coord.Row >= Rows ) return false;

				return _vertWalls[Rows * coord.Col + coord.Row];

			default:
				return false;
		}
	}

	public void SetWall( GridCoord coord, Direction dir, bool isSolid )
	{
		ForceNorthOrWest( ref coord, ref dir );

		switch ( dir )
		{
			case Direction.North:
                if (coord.Row < 0 || coord.Row > Rows || coord.Col < 0 || coord.Col >= Cols)
				{
					throw new IndexOutOfRangeException();
				}

				_horzWalls[Cols * coord.Row + coord.Col] = isSolid;
				break;

			case Direction.West:
                if (coord.Col < 0 || coord.Col > Cols || coord.Row < 0 || coord.Row >= Rows)
				{
					throw new IndexOutOfRangeException();
				}

				_vertWalls[Rows * coord.Col + coord.Row] = isSolid;
				break;
		}
	}

    public GridCoord RayCast( GridCoord from, Direction direction )
    {
        var dir = Directions[(int)direction];

        while ( true )
        {
            if ( GetWall( from, direction ) )
            {
                return from;
            }

            from += dir.Delta;
        }
    }

    public static (Direction Direction, GridCoord Delta)[] Directions { get; } = new[]
    {
        (Direction.North, new GridCoord( -1, 0 )),
        (Direction.East, new GridCoord( 0, 1 )),
        (Direction.South, new GridCoord( 1, 0 )),
        (Direction.West, new GridCoord( 0, -1 ))
    };

    public static Direction GetDirection( int dRow, int dCol )
    {
        if (Math.Abs(dCol) > Math.Abs(dRow))
        {
            return dCol > 0 ? Direction.East : Direction.West;
        }

        return dRow > 0 ? Direction.North : Direction.South;
    }

	public static Direction GetDirection( Vector3 vec )
    {
        if (Math.Abs(vec.x) > Math.Abs(vec.y))
        {
            return vec.x > 0f ? Direction.East : Direction.West;
        }

        return vec.y < 0f ? Direction.North : Direction.South;
    }

	public int GetDistance( GridCoord a, GridCoord b )
	{
		var queue = new Queue<(GridCoord Coord, int Dist)>();
		var visited = new HashSet<GridCoord> { a };

		queue.Enqueue( (a, 0) );

		while ( queue.Count > 0 )
		{
			var next = queue.Dequeue();

			if ( next.Coord == b )
			{
				return next.Dist;
			}

			foreach ( var (dir, delta) in Directions )
			{
				if ( GetWall( next.Coord, dir ) )
				{
					continue;
				}

				var neighbor = next.Coord + delta;

				if ( visited.Add( neighbor ) )
				{
					queue.Enqueue( (neighbor, next.Dist + 1) );
				}
			}
		}

		return -1;
	}

	public void Print()
	{
		for ( var row = 0; row <= Rows; ++row )
		{
			Log.Info( $"+{string.Join( "+", Enumerable.Range( 0, Cols ).Select( col => GetWall( (row, col), Direction.North ) ? "---" : "   " ) )}+" );

			if ( row >= Rows ) break;
			
			Log.Info( string.Join( "   ", Enumerable.Range( 0, Cols + 1 ).Select( col => GetWall( (row, col), Direction.West ) ? "|" : " " ) ) );
		}
	}

    public void Read( ref NetRead read )
    {
        Rows = read.Read<int>();
        Cols = read.Read<int>();

        _horzWalls = read.ReadUnmanagedArray( _horzWalls );
        _vertWalls = read.ReadUnmanagedArray( _vertWalls );
	}

    public void Write( NetWrite write )
    {
        write.Write( Rows );
		write.Write( Cols );

		write.WriteUnmanagedArray( _horzWalls );
        write.WriteUnmanagedArray( _vertWalls );
	}
}
