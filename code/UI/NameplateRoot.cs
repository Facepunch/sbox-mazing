using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Player;
using Sandbox;
using Sandbox.UI;

namespace Mazing.UI
{
    partial class NameplateRoot
    {
        public MazingPlayer Player { get; }

        public string Name => Player?.Client.Name ?? "?";
        public Label DiffText { get; set; }
        public Label ValueText { get; set; }
        public Label StreakText { get; set; }

        public Panel Avatar { get; set; }


        private readonly ValueTicker _heldTicker = new ValueTicker( "${0:N0}" );

        private readonly ValueTicker _streakTicker = new ValueTicker
        {
            ImmediateIncrease = true
        };


        private bool _wasAlive = true;
        private bool _setAvatar;

        public NameplateRoot( MazingPlayer player )
        {
            Player = player;
        }

        public override void Tick()
        {
            base.Tick();

            if ( Player == null || MazingGame.Current == null ) return;

            if ( !_setAvatar )
            {
                _setAvatar = true;
                Avatar.Style.SetBackgroundImage( $"avatar:{Player.Client.SteamId}" );
            }

            _heldTicker.SoundSource = Player;
            _heldTicker.MaxDiffOpacityValue = MazingGame.Current.TotalTreasureValue / 2;
            _heldTicker.Tick( Player.HeldCoins, ValueText, DiffText, Player.HasExited );

            _streakTicker.Tick( Player.SurvivalStreak, StreakText, null, false );

            StreakText.Style.FontColor = Player.GetSurvivalStreakColor();
            StreakText.Style.Opacity = Math.Min( Player.SurvivalStreak / 5f, 1f );

            PanelBounds = new Rect( -512f, -512f, 1024f, 512f );

            Position = Player.Position + Vector3.Up * 128f;
            Rotation = Camera.Rotation * Rotation.FromYaw( 180f );
            WorldScale = 1f;

            if ( _wasAlive != Player.IsAliveInMaze )
            {
                Style.Set( "opacity", Player.IsAliveInMaze ? "1.0" : "0.0" );
            }

            _wasAlive = Player.IsAliveInMaze;
        }
    }
}
