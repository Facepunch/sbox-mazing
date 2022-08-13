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
    internal partial class HudRoot : RootPanel
    {
        public int LevelNumber => MazingGame.Current.LevelIndex + 1;
        
        public Label TotalCoinsText { get; set; }
        public Label TotalCoinsDiffText { get; set; }

        private readonly ValueTicker _totalCoinsTicker = new ValueTicker( "${0:N0}" );

        private Panel _overlay;
        
        public HudRoot()
        {

        }
        
        public override void Tick()
        {
            base.Tick();

            var game = MazingGame.Current;
            var player = Local.Pawn as MazingPlayer;

            if ( player == null || game == null )
            {
                return;
            }

            _totalCoinsTicker.SoundSource = game.Hatch;
            
            _totalCoinsTicker.Tick( game.TotalCoins, TotalCoinsText, TotalCoinsDiffText, true, 1 );

            _overlay ??= Children.FirstOrDefault( x => x.HasClass( "overlay" ) );
            _overlay?.SetClass( "ghost", !player.IsAlive );
            _overlay?.SetClass( "transition", game.IsTransitioning );
        }
    }
}
