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

        private Panel _overlay;

        public HudRoot()
        {

        }

        public override void Tick()
        {
            base.Tick();

            var player = Local.Pawn as MazingPlayer;
            var game = MazingGame.Current;

            if ( player == null || game == null )
            {
                return;
            }

            _overlay ??= Children.FirstOrDefault( x => x.HasClass( "overlay" ) );
            _overlay?.SetClass( "ghost", !player.IsAlive );
            _overlay?.SetClass( "transition", game.IsTransitioning );
        }
    }
}
