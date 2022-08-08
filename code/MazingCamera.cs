using Sandbox;

namespace Mazing;

public class MazingCamera: CameraMode
{
	public override void Update()
	{
		if (Local.Pawn is not AnimatedEntity pawn)
			return;

		var center = pawn.Position.WithZ(0f) + Vector3.Up * 64;

		Rotation = Rotation.FromPitch(80f);

		float distance = 1600f * pawn.Scale;
		Position = center - Rotation.Forward * distance;

		FieldOfView = 20;

		Viewer = null;
	}
}
