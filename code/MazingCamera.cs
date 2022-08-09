using Sandbox;

namespace Mazing;

public class MazingCamera : CameraMode
{
	public override void Update()
    {
        Rotation = Rotation.FromYaw(90f) * Rotation.FromPitch(80f);

        if (Local.Pawn == null)
        {
            return;
        }

        var center = Local.Pawn.Position.WithZ(0f) + Vector3.Up * 64;
        var distance = 1600f * Local.Pawn.Scale;
        var target = center - Rotation.Forward * distance;

        if ( (target - Position).LengthSquared > 128f + 128f )
        {
            Position = target;
        }
        else
        {
            Position = Vector3.Lerp(Position.WithZ(target.z), target, 0.5f);
        }

		FieldOfView = 20;

		Viewer = null;
	}
}
