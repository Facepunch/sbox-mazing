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
    [UseTemplate]
    internal partial class HudRoot : RootPanel
    {
        public int LevelNumber => MazingGame.Current.LevelIndex + 1;
        
        public Label StreakBonusLabel { get; set; }
        public Label StreakBonusText { get; set; }
        public Panel StreakBonusParent { get; set; }
        
        public Label TotalCoinsText { get; set; }
        public Label TotalCoinsDiffText { get; set; }

        public Label LevelNumberText { get; set; }

        private readonly ValueTicker _totalCoinsTicker = new ValueTicker( "${0:N0}" );

        private Panel _overlay;

        private int _lastStreak = -1;
        private TimeSince _streakChangeTime;
        
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
            
            _totalCoinsTicker.Tick( game.TotalCoins, TotalCoinsText, TotalCoinsDiffText, true );

            if ( _lastStreak != player.SurvivalStreak )
            {
                _lastStreak = player.SurvivalStreak;
                _streakChangeTime = 0f;

                if ( player.SurvivalStreak > 0 )
                {
                    StreakBonusText.Text = $"{1f + player.SurvivalStreak / 5f:F1}x";
                }

                StreakBonusParent.Style.Opacity = player.SurvivalStreak > 0 ? 1f : 0f;
                StreakBonusLabel.Style.Opacity = 0.25f;
            }

            if (_streakChangeTime > 2f && StreakBonusLabel.Style.Opacity > 0.125f )
            {
                StreakBonusParent.Style.Opacity = player.SurvivalStreak > 0 ? 0.25f : 0f;
                StreakBonusLabel.Style.Opacity = 0f;
            }

            LevelNumberText.Text = $"Floor {game.LevelIndex + 1}";

            _overlay ??= Children.FirstOrDefault( x => x.HasClass( "overlay" ) );
            _overlay?.SetClass( "ghost", !player.IsAlive );
            _overlay?.SetClass( "transition", game.IsTransitioning );
        }
    }
}
