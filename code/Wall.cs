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
    Sapphire = 2,
    Ruby = 3
}

public partial class Treasure : AnimatedEntity
{
    private TreasureKind _kind;
    private PointLightEntity _light;

    [Net]
    public TreasureKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;

            if ( _light != null )
            {
                _light.Color = GetColor(value);
            }

            SetBodyGroup(0, (int)Kind);
        }
    }

    public static int GetValue( TreasureKind kind )
    {
        return kind switch
        {
            TreasureKind.Emerald => 5,
            TreasureKind.Sapphire => 20,
            TreasureKind.Ruby => 100,
            _ => 1
        };
    }

    public static Color GetColor( TreasureKind kind )
    {
        return kind switch
        {
            TreasureKind.Emerald => Color.FromRgb( 0x32cd32 ),
            TreasureKind.Sapphire => Color.FromRgb( 0x3150cd ),
            TreasureKind.Ruby => Color.FromRgb( 0x8b0000 ),
            _ => Color.FromRgb( 0xf2d873 )
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
            _light = new PointLightEntity()
            {
                Color = GetColor( 0 ),
                Brightness = 0.5f,
                Range = 48f
            };

            _light.SetParent( this, "Coin", Transform.Zero );
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
            var light = new PointLightEntity()
            {
                Color = Treasure.GetColor(0),
                Brightness = 1f,
                Range = 64f
            };

            light.SetParent(this, "Coin", Transform.Zero);
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
