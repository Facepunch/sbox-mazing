using System.Linq;
using Sandbox;

namespace Mazing;

public partial class MazingPlayer : Player
{
	public ClothingContainer Clothing = new();

	/// <summary>
	/// Default init
	/// </summary>
	public MazingPlayer()
	{
		
	}

	/// <summary>
	/// Initialize using this client
	/// </summary>
	public MazingPlayer(Client cl) : this()
	{
		// Load clothing from client data
		Clothing.LoadFromClient(cl);
	}

	public override void Respawn()
	{
		SetModel("models/citizen/citizen.vmdl");

		Controller = new MazingWalkController();

		EnableAllCollisions = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;

		Clothing.DressEntity(this);

		CameraMode = new MazingCamera();

		base.Respawn();
	}

	public override void OnKilled()
	{
		base.OnKilled();

		Controller = null;

		EnableAllCollisions = false;
		EnableDrawing = false;

		CameraMode = new SpectateRagdollCamera();

		foreach (var child in Children)
		{
			child.EnableDrawing = false;
		}
	}

	public override void Simulate(Client cl)
	{
		base.Simulate(cl);

		if (Input.ActiveChild != null)
		{
			ActiveChild = Input.ActiveChild;
		}

		if (LifeState != LifeState.Alive)
			return;

		var controller = GetActiveController();
		if (controller != null)
		{
			EnableSolidCollisions = !controller.HasTag("noclip");

			SimulateAnimation(controller);
		}

		TickPlayerUse();
		SimulateActiveChild(cl, ActiveChild);

		//if (Input.Pressed(InputButton.View))
		//{
		//	if (CameraMode is MazingCamera)
		//	{
		//		CameraMode = new FirstPersonCamera();
		//	}
		//	else
		//	{
		//		CameraMode = new MazingCamera();
		//	}
		//}

		if (Input.Released(InputButton.Jump))
        {
            var hatch = Entity.All.OfType<Hatch>().First();

			hatch.Open();
        }
	}

	void SimulateAnimation(PawnController controller)
	{
		if (controller == null)
			return;

		// where should we be rotated to
		var turnSpeed = 0.02f;
		var idealRotation = Rotation.LookAt(Controller.WishVelocity.WithZ(0), Vector3.Up);
		Rotation = Rotation.Slerp(Rotation, idealRotation, Controller.WishVelocity.Length * Time.Delta * turnSpeed);
		// Rotation = Rotation.Clamp(idealRotation, 45.0f, out var shuffle); // lock facing to within 45 degrees of look direction

		CitizenAnimationHelper animHelper = new CitizenAnimationHelper(this);

		animHelper.WithWishVelocity(controller.WishVelocity);
		animHelper.WithVelocity(controller.Velocity);
		animHelper.WithLookAt(EyePosition + Rotation.Forward * 100.0f, 1.0f, 1.0f, 0.5f);
		animHelper.AimAngle = Rotation;
		animHelper.FootShuffle = 0f;
		animHelper.DuckLevel = MathX.Lerp(animHelper.DuckLevel, controller.HasTag("ducked") ? 1 : 0, Time.Delta * 10.0f);
		animHelper.IsGrounded = GroundEntity != null;
		animHelper.IsSitting = controller.HasTag("sitting");
		animHelper.IsNoclipping = controller.HasTag("noclip");
		animHelper.IsClimbing = controller.HasTag("climbing");
		animHelper.IsSwimming = WaterLevel >= 0.5f;
		animHelper.IsWeaponLowered = false;

		if (controller.HasEvent("jump")) animHelper.TriggerJump();
		//if (ActiveChild != lastWeapon) animHelper.TriggerDeploy();

		if (ActiveChild is BaseCarriable carry)
		{
			carry.SimulateAnimator(animHelper);
		}
		else
		{
            animHelper.HoldType = CitizenAnimationHelper.HoldTypes.None;
			animHelper.AimBodyWeight = 0.5f;
		}
	}

	public override void StartTouch(Entity other)
	{
		base.StartTouch(other);

	}

	public override float FootstepVolume()
	{
		return Velocity.WithZ(0).Length.LerpInverse(0.0f, 200.0f) * 5.0f;
	}
}