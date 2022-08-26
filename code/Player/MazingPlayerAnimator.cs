using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Enemies;
using Sandbox;

namespace Mazing.Player;

internal partial class MazingPlayerAnimator : PawnAnimator
{
	TimeSince TimeSinceFootShuffle = 60;


	float duck;

    public override void Simulate()
	{
		var player = Pawn as MazingPlayer;
		var idealRotation = Rotation.LookAt(EyeRotation.Forward.WithZ(0), Vector3.Up);

		DoRotation(idealRotation);
		DoWalk();

        //
		// Let the animation graph know some shit
		//
        bool held = HasTag( "held" );
		bool sitting = HasTag("sitting");
		bool isGhost = !(player?.IsAlive ?? true);

        SetAnimParameter("b_grounded", !isGhost && (GroundEntity != null || held));
		SetAnimParameter("b_noclip", isGhost && Velocity.WithZ( 0f ).LengthSquared < 50f * 50f);
		SetAnimParameter("b_sit", sitting);
		SetAnimParameter("b_swim", Pawn.WaterLevel > 0.5f && !sitting);

		if (Host.IsClient && Client.IsValid())
		{
			SetAnimParameter("voice", Client.TimeSinceLastVoice < 0.5f ? Client.VoiceLevel : 0.0f);
		}

		if (HasTag("ducked") || held) duck = duck.LerpTo(1.0f, Time.Delta * 10.0f);
		else duck = duck.LerpTo(0.0f, Time.Delta * 5.0f);
		SetAnimParameter("duck", duck);

		Vector3 lookPos;

		if ( player != null )
		{
			lookPos = Pawn.EyePosition + EyeRotation.Forward * 200;

			if ( player.HeldEntity != null )
			{
				/*
                SetAnimParameter("holdtype", 4);
                SetAnimParameter("holdtype_handedness", 1);
				SetAnimParameter("b_vr", false);
                SetAnimParameter("aim_body_weight", 1f);
				*/

				SetAnimParameter( "b_vr", true );
                SetAnimParameter("aim_body_weight", 0.75f);
                SetAnimParameter( "left_hand_ik.position", new Vector3( 6f, 14f, 64f ) );
                SetAnimParameter( "right_hand_ik.position", new Vector3( 6f, -14f, 64f ) );

                player.SetAnimParameter( "left_hand_ik.rotation", Rotation.From( -65f, 87f, 7f ) );
                player.SetAnimParameter( "right_hand_ik.rotation", Rotation.From( -115f, 87f, 7f ) );
			}
            else
            {
                SetAnimParameter("holdtype", 0);
                SetAnimParameter( "b_vr", false );
				SetAnimParameter("aim_body_weight", 0.5f);
            }
		} 
		else
        {
            if (Pawn is Enemy enemy)
            {
				lookPos = enemy.LookPos;
			} 
			else
            {
				lookPos = Pawn.EyePosition + EyeRotation.Forward * 200;
			}
		}

		SetLookAt("aim_eyes", lookPos);
		SetLookAt("aim_head", lookPos);
		SetLookAt("aim_body", Pawn.EyePosition + EyeRotation.Forward * 200);
		//SetLookAt("aim_body", lookPos);
	}

	public virtual void DoRotation( Rotation idealRotation )
	{
		var player = Pawn as MazingPlayer;

		//
		// Our ideal player model rotation is the way we're facing
		//
		var allowYawDiff = player?.ActiveChild == null ? 90 : 50;

		float turnSpeed = 20f;

        //
		// If we're moving, rotate to our ideal rotation
		//
		Rotation = Rotation.Slerp(Rotation, idealRotation, Time.Delta * turnSpeed);

		//
		// Clamp the foot rotation to within 120 degrees of the ideal rotation
		//
		Rotation = Rotation.Clamp(idealRotation, allowYawDiff, out var change);

		//
		// If we did restrict, and are standing still, add a foot shuffle
		//
		if (change > 1 && WishVelocity.Length <= 1) TimeSinceFootShuffle = 0;

		SetAnimParameter("b_shuffle", TimeSinceFootShuffle < 0.1);
	}

	void DoWalk()
    {
        var velocity = Velocity;
        var wishVelocity = WishVelocity;

        if (HasTag("held"))
        {
            velocity = Vector3.Zero;
            wishVelocity = Vector3.Zero;
		}

		// Move Speed
		{
			var dir = velocity;
			var forward = Rotation.Forward.Dot(dir);
			var sideward = Rotation.Right.Dot(dir);

			var angle = MathF.Atan2(sideward, forward).RadianToDegree().NormalizeDegrees();

			SetAnimParameter("move_direction", angle);
			SetAnimParameter("move_speed", velocity.Length);
			SetAnimParameter("move_groundspeed", velocity.WithZ(0).Length);
			SetAnimParameter("move_y", sideward);
			SetAnimParameter("move_x", forward);
			SetAnimParameter("move_z", velocity.z);
		}

		// Wish Speed
		{
			var dir = wishVelocity;
			var forward = Rotation.Forward.Dot(dir);
			var sideward = Rotation.Right.Dot(dir);

			var angle = MathF.Atan2(sideward, forward).RadianToDegree().NormalizeDegrees();

			SetAnimParameter("wish_direction", angle);
			SetAnimParameter("wish_speed", wishVelocity.Length);
			SetAnimParameter("wish_groundspeed", wishVelocity.WithZ(0).Length);
			SetAnimParameter("wish_y", sideward);
			SetAnimParameter("wish_x", forward);
			SetAnimParameter("wish_z", wishVelocity.z);
		}
	}

	public override void OnEvent( string name )
    {
        // DebugOverlay.Text( Pos + Vector3.Up * 100, name, 5.0f );

		if (name == "vault")
		{
			Trigger("b_jump");
		}

		base.OnEvent(name);
	}
}