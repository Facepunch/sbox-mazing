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

        SetModel( "models/border_plane.vmdl" );

        Tags.Add("border");

        EnableDrawing = true;
    }
}

public partial class Wall : ModelEntity
{
	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/wall.vmdl" );

        Tags.Add("wall");
        Tags.Remove("solid");

		EnableDrawing = true;
	}
}

public partial class Post : ModelEntity
{
	public override void Spawn()
	{
		base.Spawn();

        SetModel( "models/post.vmdl" );

        Tags.Add("wall");
        Tags.Remove( "solid" );

		EnableDrawing = true;
	}
}

public partial class Coin : AnimatedEntity
{
    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/coin.vmdl");

        Tags.Add( "coin" );

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }
}

public partial class Key : ModelEntity
{
    [Net]
    public bool IsHeld { get; set; }

    public Vector3 TargetPosition { get; set; }

    private bool _firstTick;

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/key.vmdl");

        Tags.Add( "key" );

        if ( IsServer )
        {
            var light = new PointLightEntity
            {
                Color = Color.FromRgb( 0xf2d873 ),
                Brightness = 1f,
                Range = 128f
            };

            light.Parent = this;
        }

        Scale = 0.25f;

        _firstTick = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
	}

    [Event.Tick.Server]
    public void ServerTick()
    {
        if ( _firstTick )
        {
            _firstTick = false;
            TargetPosition = LocalPosition;
        }

        LocalPosition += (TargetPosition - LocalPosition).WithZ( 0f ) * 0.125f;

        LocalPosition = LocalPosition.WithZ( 0f ) + Vector3.Up * (MathF.Sin( Time.Now * MathF.PI * 0.5f ) * 16f + (IsHeld ? 96f : 32f));
        LocalRotation *= Rotation.FromRoll( Time.Delta * 180f ) * Rotation.FromYaw(Time.Delta * 80f);
    }
}
