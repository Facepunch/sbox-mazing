using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.UI;

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

    [ClientRpc]
    public static void ClientOpenNotify( string name )
    {
        if (!string.IsNullOrEmpty(name))
        {
            ChatBox.AddInformation( $"{name} has unlocked the exit hatch!" );
        }
        else
        {
            ChatBox.AddInformation( "The exit hatch has been unlocked!" );
        }
    }

    public void Open()
    {
        if ( IsOpen || !IsServer )
        {
            return;
        }

        ClientOpenNotify( MazingGame.Current.Key?.LastHolder != null
            ? MazingGame.Current.Key.LastHolder.Client.Name
            : null );

        Sound.FromEntity( "hatch.open", this );

        IsOpen = true;

        Tags.Remove( "solid" );
        Tags.Add( "exit" );

        PlaybackRate = 1f;
        CurrentSequence.Time = 0f;
    }
}