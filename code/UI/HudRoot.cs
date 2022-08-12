using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.UI;

namespace Mazing.UI
{
    public class ValueTicker
    {
        public const int MinCoinAccumulateRate = 1;

        private int _lastValue;
        private int _spareValue;
        private int _diffSign;

        private TimeSince _changedTime;

        private bool _firstUpdate = true;

        private void UpdateText( Label valueText, Label diffText )
        {
            valueText.Text = $"{_lastValue - _spareValue}";
            diffText.Text = $"{(_diffSign > 0 ? "+" : "-")}{Math.Abs( _spareValue )}";

            diffText.Style.Set( "left",
                $"{15 + (int)Math.Floor( Math.Log10( Math.Max( 1, _lastValue - _spareValue ) ) ) * 3}vh" );
        }

        public void Tick( int value, Label valueText, Label diffText, bool ignoreDecrease )
        {
            if ( _firstUpdate )
            {
                _firstUpdate = false;
                valueText.Text = value.ToString();
            }

            if (value != _lastValue)
            {
                var diff = value - _lastValue;
                _lastValue = value;

                diffText.SetClass( "negative", diff < 0 );

                if ( diff > 0 || !ignoreDecrease )
                {
                    diffText.Style.Set( "opacity", "1.0" );
                }
                else
                {
                    diffText.Style.Set( "opacity", "0.0" );
                    _spareValue = 0;
                }

                _spareValue += diff;
                _diffSign = Math.Sign(_spareValue);

                _changedTime = -1f;

                UpdateText(valueText, diffText);
            }

            if (_changedTime >= 0f && _spareValue != 0)
            {
                _changedTime = -0.5f / Math.Abs(_spareValue);

                _spareValue -= Math.Sign( _spareValue ) * Math.Max( MinCoinAccumulateRate, Math.Abs( _spareValue ) / 20 );

                Sound.FromScreen( "click.tiny", 1f, 0f );

                if (Math.Abs(_spareValue) < MinCoinAccumulateRate)
                {
                    _spareValue = 0;
                }

                UpdateText(valueText, diffText);
            }

            if (_changedTime > 0.5f)
            {
                diffText.Style.Set("opacity", "0.0");
            }

        }
    }

    [UseTemplate]
    internal partial class HudRoot : RootPanel
    {
        public int LevelNumber => MazingGame.Current.LevelIndex + 1;

        public Label HeldCoinsText { get; set; }
        public Label HeldCoinsDiffText { get; set; }

        public Label TotalCoinsText { get; set; }
        public Label TotalCoinsDiffText { get; set; }

        private readonly ValueTicker _heldCoinsTicker = new ValueTicker();
        private readonly ValueTicker _totalCoinsTicker = new ValueTicker();

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

            _heldCoinsTicker.Tick( player.HeldCoins, HeldCoinsText, HeldCoinsDiffText, player.HasExited );
            _totalCoinsTicker.Tick( game.TotalCoins, TotalCoinsText, TotalCoinsDiffText, true );

            _overlay ??= Children.FirstOrDefault( x => x.HasClass( "overlay" ) );
            _overlay?.SetClass( "ghost", !player.IsAlive );
            _overlay?.SetClass( "transition", game.IsTransitioning );
        }
    }
}
