using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing;

public partial class Wall : ModelEntity
{
	public override void Spawn()
	{
		base.Spawn();

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

		SetModel( "models/post.vmdl" );

		UsePhysicsCollision = true;

		EnableDrawing = true;
		EnableSolidCollisions = true;
		EnableAllCollisions = true;
	}
}
