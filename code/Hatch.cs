using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing;

public partial class Hatch : AnimatedEntity
{
    [Net]
    public bool IsOpen { get; private set; }

    public override void Spawn()
    {
        base.Spawn();

        SetModel( "models/hatch.vmdl" );

        Rotation = Rotation.FromYaw( 270 );

        PlaybackRate = 0f;
        CurrentSequence.Time = 0f;

        UsePhysicsCollision = true;

        EnableDrawing = true;
    }
    
    public void Open()
    {
        if ( IsOpen || !IsServer )
        {
            return;
        }

        IsOpen = true;

        Tags.Remove( "solid" );
        Tags.Add( "exit" );

        PlaybackRate = 1f;
        CurrentSequence.Time = 0f;
    }
}