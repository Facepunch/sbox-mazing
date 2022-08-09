using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mazing;

internal class PathFinder
{
    private readonly PriorityQueue<(int Row, int Col), float> _openSet = new();
    private readonly Dictionary<(int Row, int Col), (int Row, int Col)> _cameFrom = new();
    private readonly Dictionary<(int Row, int Col), float> _gScore = new();
    private readonly Dictionary<(int Row, int Col), float> _fScore = new();

    public float ClosedHatchCost { get; set; } = 1f;
    public float OpenHatchCost { get; set; } = float.PositiveInfinity;
    public float PlayerCost { get; set; } = 10f;
    public float EnemyCost { get; set; } = 10f;

    private static float Heuristic( (int Row, int Col) from, (int Row, int Col) to )
    {
        var dRow = to.Row - from.Row;
        var dCol = to.Col - from.Col;

        return Math.Abs( dRow ) + Math.Abs( dCol );
    }

    private void ReconstructPath( (int Row, int Col) from, (int Row, int Col) to, List<(int Row, int Col)> outPath )
    {
        var next = to;
        var startIndex = outPath.Count;

        while ( true )
        {
            outPath.Add( next );
            if ( next == from ) break;

            next = _cameFrom[next];
        }

        outPath.Reverse( startIndex, outPath.Count - startIndex );
    }

    public bool FindPath( (int Row, int Col) from, (int Row, int Col) to, List<(int Row, int Col)> outPath )
    {
        if ( from == to )
        {
            outPath.Add( to );
            return true;
        }

        _openSet.Clear();
        _openSet.Enqueue( from, Heuristic( from, to ) );

        _cameFrom.Clear();

        _gScore.Clear();
        _gScore.Add( from, 0f );

        _fScore.Clear();
        _fScore.Add( from, Heuristic( from, to ) );

        var maze = MazingGame.Current.CurrentMaze;

        while ( _openSet.TryDequeue( out var current, out var fScore ) )
        {
            if ( current == to )
            {
                ReconstructPath( from, to, outPath );
                return true;
            }

            foreach ( var (dir, dRow, dCol) in MazeData.Directions )
            {
                if ( maze.GetWall( current.Row, current.Col, dir ) )
                {
                    continue;
                }

                var next = (current.Row + dRow, current.Col + dCol);

                var gScoreNext = _gScore[current] + 1f;
                if ( !_gScore.TryGetValue( next, out var gScoreOld ) || gScoreNext < gScoreOld )
                {
                    var fScoreNext = gScoreNext + Heuristic(next, to);

                    _cameFrom[next] = current;

                    _gScore[next] = gScoreNext;
                    _fScore[next] = fScoreNext;

                    _openSet.Enqueue( next, fScoreNext );
                }
            }
        }

        return false;
    }
}
