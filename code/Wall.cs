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

        UsePhysicsCollision = true;

		EnableDrawing = true;
        EnableSolidCollisions = true;
    }
}

public partial class Post : ModelEntity
{
	public override void Spawn()
	{
		base.Spawn();

        SetModel( "models/post.vmdl" );

        Tags.Add("wall");
        
        UsePhysicsCollision = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }
}

public enum TreasureKind
{
    Emerald = 1,
    Sapphire = 2
}

public partial class Treasure : AnimatedEntity
{
    private TreasureKind _kind;

    [Net]
    public TreasureKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            SetBodyGroup(0, (int)Kind);
        }
    }

    public static int GetValue( TreasureKind kind )
    {
        return kind switch
        {
            TreasureKind.Emerald => 5,
            TreasureKind.Sapphire => 20,
            _ => 1
        };
    }

    public int Value => GetValue( Kind );

    public Treasure()
    {

    }

    public Treasure( TreasureKind kind )
    {
        Kind = kind;
    }

    public override void Spawn()
    {
        base.Spawn();

        SetModel( "models/item.vmdl" );

        Tags.Add( "coin" );

        if (IsServer)
        {
            var light = new SpotLightEntity
            {
                Color = Color.FromRgb(0xf2d873),
                Brightness = 32f,
                Range = 1024f,
                Parent = this,
                InnerConeAngle = 0f,
                OuterConeAngle = 4f,
                LocalPosition = Vector3.Up * 256f,
                LocalRotation = Rotation.FromPitch( 90f )
            };
        }


        EnableDrawing = true;
        EnableSolidCollisions = true;
    }
}

public abstract partial class Holdable : AnimatedEntity
{
    public bool IsHeld => Parent is MazingPlayer;

    protected MazingGame Game => MazingGame.Current;

    public Vector3 TargetPosition { get; set; }

    private bool _firstTick;

    public override void Spawn()
    {
        base.Spawn();

        _firstTick = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }

    [Event.Tick.Server]
    public void ServerTick()
    {
        if (_firstTick)
        {
            _firstTick = false;
            TargetPosition = LocalPosition.WithZ( 0f );
        }

        LocalPosition += (TargetPosition - LocalPosition) * 0.125f;

        // Don't tick if moving to target position
        if ((TargetPosition - LocalPosition).LengthSquared > 4f * 4f)
        {
            return;
        }

        OnServerTick();
    }

    protected virtual void OnServerTick()
    {
        if ( IsHeld )
        {
            return;
        }
        
        var closestPlayer = Game.GetClosestPlayer(Position, 20f);

        if (closestPlayer != null && closestPlayer.CanPickUpItem)
        {
            closestPlayer.PickUpItem( this );
        }
    }
}

public partial class Key : Holdable
{
    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/item.vmdl");
        SetBodyGroup( 0, 0 );

        Tags.Add( "key" );
        
        if (IsServer)
        {
            var light = new SpotLightEntity
            {
                Color = Color.FromRgb(0xf2d873),
                Brightness = 32f,
                Range = 1024f,
                Parent = this,
                InnerConeAngle = 0f,
                OuterConeAngle = 7.5f,
                LocalPosition = Vector3.Up * 256f,
                LocalRotation = Rotation.FromPitch(90f)
            };
        }
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        var hatch = MazingGame.Current.Hatch;

        if (hatch == null || hatch.IsOpen)
        {
            return;
        }

        var diff = hatch.Position.WithZ(0) - Position.WithZ(0);

        if (diff.LengthSquared < 16f * 16f)
        {
            hatch.Open();
            Delete();
        }
    }
}
