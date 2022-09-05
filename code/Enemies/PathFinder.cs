using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Enemies;
using Sandbox;

namespace Mazing.Enemies;

internal class PathFinder
{
    private readonly PriorityQueue<GridCoord, float> _openSet = new();
    private readonly Dictionary<GridCoord, GridCoord> _cameFrom = new();
    private readonly Dictionary<GridCoord, float> _gScore = new();
    private readonly Dictionary<GridCoord, float> _fScore = new();

    private readonly Dictionary<GridCoord, float> _costs = new();

    public float ClosedHatchCost { get; set; } = 1f;
    public float OpenHatchCost { get; set; } = 100f;
    public float PlayerCost { get; set; } = 10f;
    public float EnemyCost { get; set; } = 10f;

    private static float Heuristic( GridCoord from, GridCoord to )
    {
        var dRow = to.Row - from.Row;
        var dCol = to.Col - from.Col;

        return Math.Abs( dRow ) + Math.Abs( dCol );
    }

    private void ReconstructPath( GridCoord from, GridCoord to, List<GridCoord> outPath )
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

    private void AddCost( GridCoord cell, float cost )
    {
        if ( cost <= 0f ) return;

        if ( _costs.TryGetValue( cell, out var oldCost ) )
        {
            _costs[cell] = oldCost + cost;
        }
        else
        {
            _costs[cell] = cost;
        }
    }
    
    public bool FindPath( GridCoord from, GridCoord to, List<GridCoord> outPath )
    {
        if ( from == to )
        {
            outPath.Add( to );
            return true;
        }

        var game = MazingGame.Current;

        if (!game.CurrentMaze.IsConnected(from, to))
        {
            return false;
        }

        _openSet.Clear();
        _openSet.Enqueue( from, Heuristic( from, to ) );

        _cameFrom.Clear();

        _gScore.Clear();
        _gScore.Add( from, 0f );

        _fScore.Clear();
        _fScore.Add( from, Heuristic( from, to ) );

        _costs.Clear();

        AddCost( game.Hatch.GetCellIndex(), game.Hatch.IsOpen ? OpenHatchCost : ClosedHatchCost );

        foreach ( var player in game.PlayersAliveInMaze )
        {
            AddCost( player.GetCellIndex(), PlayerCost );
        }

        foreach ( var enemy in game.Enemies )
        {
            AddCost( enemy.GetCellIndex(), EnemyCost );
        }

        var maze = game.CurrentMaze;

        while ( _openSet.TryDequeue( out var current, out var fScore ) )
        {
            if ( current == to )
            {
                ReconstructPath( from, to, outPath );
                return true;
            }

            foreach ( var (dir, delta) in MazeData.Directions )
            {
                if ( maze.GetWall( current, dir ) )
                {
                    continue;
                }

                var next = current + delta;

                var gScoreNext = _gScore[current] + 1f;

                if ( _costs.TryGetValue( next, out var addCost ) )
                {
                    gScoreNext += addCost;
                }

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
