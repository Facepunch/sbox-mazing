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
    partial class HudRoot
    {
        public int LevelNumber => MazingGame.Current.LevelIndex + 1;
        
        public Label StreakBonusLabel { get; set; }
        public Label StreakBonusText { get; set; }
        public Panel StreakBonusParent { get; set; }
        
        public Label TotalCoinsText { get; set; }
        public Label TotalCoinsDiffText { get; set; }

        public Label LevelNumberText { get; set; }

        public Label EditorWarning { get; set; }
        public Label DailyChallengeInfo { get; set; }

        public Label LevelTime { get; set; }
        public Label TotalTime { get; set; }

        private readonly ValueTicker _totalCoinsTicker = new ValueTicker( "${0:N0}" );

        private Panel _overlay;

        private int _lastStreak = -1;
        private TimeSince _streakChangeTime;

        private int _lastLevelTimeSeconds = -1;
        private int _lastTotalTimeSeconds = -1;

        private bool _wasDailyEnabled;
        private bool _hasDailyEnded;

        public HudRoot()
        {

        }

        private bool UpdateTimeText(Label label, TimeSpan time, ref int lastSeconds)
        {
            var seconds = (int)time.TotalSeconds;

            if (seconds == lastSeconds) return false;

            lastSeconds = seconds;
            label.Text = time.ToString("mm\\:ss");

            return true;
        }

        public override void Tick()
        {
            base.Tick();

            var game = MazingGame.Current;
            var player = Game.LocalPawn as MazingPlayer;

            if ( player == null || game == null )
            {
                return;
            }

            if (DailyChallengeInfo != null)
            {
                if (game.DailyChallengeEnabled != _wasDailyEnabled || game.DailyChallengeComplete != _hasDailyEnded)
                {
                    _wasDailyEnabled = game.DailyChallengeEnabled;
                    _hasDailyEnded = game.DailyChallengeComplete;

                    DailyChallengeInfo.Style.Display = DisplayMode.Flex;
                    DailyChallengeInfo.Text = game.DailyChallengeComplete
                        ? "DAILY CHALLENGE COMPLETE, COME BACK TOMORROW!"
                        : player.IsSpectatorOnly
                            ? "DAILY CHALLENGE IN PROGRESS, YOU ARE SPECTATING"
                            : $"DAILY CHALLENGE {game.DailyChallengeDateUtc.Year}-{game.DailyChallengeDateUtc.Month:00}-{game.DailyChallengeDateUtc.Day:00}";
                }
            }

            LevelTime.Parent.Style.Opacity = game.LevelIndex > 0 ? 1f : 0f;

            if (UpdateTimeText(LevelTime, game.LevelTime, ref _lastLevelTimeSeconds))
            {
                UpdateTimeText(TotalTime, game.TotalTime, ref _lastTotalTimeSeconds);
            }

            EditorWarning.Style.Display = game.IsEditorMode ? DisplayMode.Flex : DisplayMode.None;

            _totalCoinsTicker.SoundSource = game.Hatch;
            
            _totalCoinsTicker.Tick( game.TotalCoins, TotalCoinsText, TotalCoinsDiffText, true );

            if ( _lastStreak != player.SurvivalStreak )
            {
                _lastStreak = player.SurvivalStreak;
                _streakChangeTime = 0f;

                if ( player.SurvivalStreak > 0 )
                {
                    StreakBonusText.Text = $"{player.GetSurvivalStreakBonus():F1}x";
                }

                StreakBonusText.Style.FontColor = player.GetSurvivalStreakColor();

                StreakBonusParent.Style.Opacity = player.SurvivalStreak > 0 ? 1f : 0f;
                StreakBonusLabel.Style.Opacity = 0.25f;
            }

            if (_streakChangeTime > 2f && StreakBonusLabel.Style.Opacity > 0.125f )
            {
                StreakBonusParent.Style.Opacity = player.SurvivalStreak > 0 ? 0.25f : 0f;
                StreakBonusLabel.Style.Opacity = 0f;
            }

            LevelNumberText.Text = $"Floor {game.LevelIndex + 1} of {MazingGame.TotalLevelCount}";

            _overlay ??= Children.FirstOrDefault( x => x.HasClass( "overlay" ) );
            _overlay?.SetClass( "ghost", !player.IsAlive );
            _overlay?.SetClass( "transition", game.IsTransitioning );
        }
    }
}
