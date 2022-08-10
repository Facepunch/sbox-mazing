using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing;

public partial class Border : ModelEntity
{
    public override void Spawn()
    {
        base.Spawn();

        Tags.Add( "border" );

        SetModel( "models/border_plane.vmdl" );

        EnableDrawing = true;
    }
}

public partial class Wall : ModelEntity
{
	public override void Spawn()
	{
		base.Spawn();

        Tags.Add( "wall" );

		SetModel( "models/wall.vmdl" );

		UsePhysicsCollision = true;

		EnableDrawing = true;
		EnableSolidCollisions = true;
		EnableAllCollisions = true;
	}
}

public partial class Post : ModelEntity
{
	public override void Spawn()
	{
		base.Spawn();

        Tags.Add( "wall" );

        SetModel( "models/post.vmdl" );

		UsePhysicsCollision = true;

		EnableDrawing = true;
		EnableSolidCollisions = true;
		EnableAllCollisions = true;
	}
}

public partial class Key : ModelEntity
{
    [Net]
    public bool IsHeld { get; set; }

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/key.vmdl");

        if ( IsServer )
        {
            var light = new PointLightEntity
            {
                Color = Color.FromRgb( 0xf2d873 ),
                Range = 64f
            };

            light.Parent = this;
        }

        Scale = 0.25f;
		
        EnableDrawing = true;
        EnableSolidCollisions = true;
	}

    [Event.Tick.Server]
    public void ServerTick()
    {
        LocalPosition = LocalPosition.WithZ( 0f ) + Vector3.Up * (MathF.Sin( Time.Now * MathF.PI * 0.5f ) * 16f + (IsHeld ? 96f : 64f));
        LocalRotation *= Rotation.FromRoll( Time.Delta * 180f ) * Rotation.FromYaw(Time.Delta * 80f);
    }
}
