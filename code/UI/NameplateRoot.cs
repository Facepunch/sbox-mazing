using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.UI;

namespace Mazing.UI
{
    [UseTemplate]
    public partial class NameplateRoot : WorldPanel
    {
        public MazingPlayer Player { get; }

        public string Name => Player?.Client.Name ?? "?";
        public Label DiffText { get; set; }
        public Label ValueText { get; set; }

        public Panel Avatar { get; set; }


        private readonly ValueTicker _ticker = new ValueTicker( "${0:N0}" );


        private bool _wasAlive = true;

        public NameplateRoot( MazingPlayer player )
        {
            Player = player;
        }

        public override void Tick()
        {
            base.Tick();

            if ( Player == null || MazingGame.Current == null ) return;

            var camera = (Local.Pawn as Player)?.CameraMode;

            if ( camera == null ) return;

            Avatar.Style.SetBackgroundImage( $"avatar:{Player.Client.PlayerId}" );

            _ticker.SoundSource = Player;
            _ticker.Tick( Player.HeldCoins, ValueText, DiffText, Player.HasExited, MazingGame.Current.TotalTreasureValue / 2 );
            
            PanelBounds = new Rect(-512f, -512f, 1024f, 512f);

            Position = Player.Position + Vector3.Up * 128f;
            Rotation = camera.Rotation * Rotation.FromYaw( 180f );

            if ( _wasAlive != Player.IsAliveInMaze )
            {
                Style.Set( "opacity", Player.IsAliveInMaze ? "1.0" : "0.0" );
            }

            _wasAlive = Player.IsAliveInMaze;
        }
    }
}
