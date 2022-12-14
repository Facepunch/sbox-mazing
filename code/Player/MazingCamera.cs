using System;
using System.Linq;
using Sandbox;
using Sandbox.Internal;

namespace Mazing.Player;

partial class MazingPlayer
{
    public const float LevelEdgeMargin = 64f;
    public const float AroundPlayerMargin = 48f;

    public (Vector3 Min, Vector3 Max) GetCameraBounds()
    {
        var game = MazingGame.Current;

        var a = game.CellCenterToPosition( (0, 0) );
        var b = game.CellCenterToPosition( (game.CurrentMaze.Rows - 1, game.CurrentMaze.Cols - 1) );

        var min = Vector3.Min( a, b ) + new Vector3( LevelEdgeMargin, LevelEdgeMargin );
        var max = Vector3.Max( a, b ) - new Vector3( LevelEdgeMargin, LevelEdgeMargin );

        if ( min.x > max.x )
        {
            min.x = max.x = (min.x + max.x) * 0.5f;
        }

        if ( min.y > max.y )
        {
            min.y = max.y = (min.y + max.y) * 0.5f;
        }

        return (min, max);
    }

    private static float CalculateVerticalFoV( float horzFoV, float aspectRatio )
    {
        return 360 * MathF.Atan( MathF.Tan( horzFoV * MathF.PI / 360f ) * aspectRatio ) / MathF.PI;
    }

    private void FrameSimulateCamera( IClient client )
    {
        Camera.Rotation = Rotation.FromYaw( 90f ) * Rotation.FromPitch( 80f );

        var targetPawn = this;

        if ( targetPawn.HasExited && targetPawn.ExitTime > 1.5f )
        {
            targetPawn = MazingGame.Current.PlayersAliveInMaze.FirstOrDefault();
        }

        if ( targetPawn == null )
        {
            return;
        }

        var center = targetPawn.Position.WithZ( 64f );
        var distance = 1600f * targetPawn.Scale;
        var target = (Camera.Position + Camera.Rotation.Forward * distance).WithZ( center.z );
        var (stageMin, stageMax) = GetCameraBounds();

        target.x = Math.Clamp( target.x, center.x - AroundPlayerMargin, center.x + AroundPlayerMargin );
        target.y = Math.Clamp( target.y, center.y - AroundPlayerMargin, center.y + AroundPlayerMargin );

        target.x = Math.Clamp( target.x, stageMin.x, stageMax.x );
        target.y = Math.Clamp( target.y, stageMin.y, stageMax.y );

        Camera.Position = target - Camera.Rotation.Forward * distance;

        Sound.Listener = new Transform( targetPawn.EyePosition, Rotation.LookAt( Camera.Rotation.Forward.WithZ( 0f ), Vector3.Up ) );

        Camera.FieldOfView = CalculateVerticalFoV( 20f, Screen.Width / Screen.Height );
    }
}
