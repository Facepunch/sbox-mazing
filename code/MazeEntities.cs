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

        Tags.Add( "border" );

        EnableDrawing = true;
    }
}

public partial class MazeEntity : ModelEntity
{
    public override void Spawn()
    {
        base.Spawn();

        Tags.Add( "solid" );

        UsePhysicsCollision = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }
}

public partial class Wall : MazeEntity
{
    public override void Spawn()
    {
        SetModel( "models/wall.vmdl" );

        base.Spawn();
    }
}

public partial class Post : MazeEntity
{
    public override void Spawn()
    {
        SetModel( "models/post.vmdl" );

        base.Spawn();
    }
}

public static class EntityExtensions
{
    public static (float Row, float Col) GetCell( this Entity entity ) => MazingGame.Current.PositionToCell( entity.Position );
    public static GridCoord GetCellIndex( this Entity entity ) => MazingGame.Current.PositionToCellIndex( entity.Position );
    public static Direction GetFacingDirection( this Entity entity ) => MazeData.GetDirection( entity.AimRay.Forward );
}