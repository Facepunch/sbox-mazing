using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing;

internal partial class Hatch : AnimatedEntity
{
    [Net]
    public bool IsOpen { get; private set; }

    public override void Spawn()
    {
        base.Spawn();

        SetModel( "models/hatch.vmdl" );

        Rotation = Rotation.FromYaw( 180 );

        PlaybackRate = 0f;
        CurrentSequence.Time = 0f;

        UsePhysicsCollision = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
        EnableAllCollisions = true;
    }
    
    public void Open()
    {
        if ( IsOpen || !IsServer )
        {
            return;
        }

        IsOpen = true;

        PlaybackRate = 1f;
        CurrentSequence.Time = 0f;

        EnableAllCollisions = false;
    }
}