using Sandbox;

namespace Mazing;

public class MazingCamera: CameraMode
{
	public override void Update()
	{
		if (Local.Pawn is not AnimatedEntity pawn)
			return;

		var center = pawn.Position.WithZ(0f) + Vector3.Up * 64;

        Rotation = Rotation.FromYaw(90f) * Rotation.FromPitch( 80f );

		var distance = 1600f * pawn.Scale;
        var target = center - Rotation.Forward * distance;

		Position = Vector3.Lerp( Position.WithZ( target.z ), target, 0.5f );

		FieldOfView = 20;

		Viewer = null;
	}
}
