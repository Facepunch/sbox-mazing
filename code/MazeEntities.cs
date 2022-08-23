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

        //Tags.Add("wall");

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

        SetupPhysicsFromCapsule( PhysicsMotionType.Static, Capsule.FromHeightAndRadius( 256f, 4f ) );

        //Tags.Add("wall");
        
        UsePhysicsCollision = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }
}

public static class EntityExtensions
{
    public static (float Row, float Col) GetCell( this Entity entity ) => MazingGame.Current.PositionToCell(entity.Position);
    public static GridCoord GetCellIndex( this Entity entity ) => MazingGame.Current.PositionToCellIndex(entity.Position);
    public static Direction GetFacingDirection( this Entity entity ) => MazeData.GetDirection(entity.EyeRotation.Forward);
}
