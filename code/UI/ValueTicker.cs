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
        private string _format;

        public Entity SoundSource { get; set; }

        public bool ImmediateIncrease { get; set; }

        public int MaxDiffOpacityValue { get; set; } = 1;
        public float MinDiffOpacity { get; set; } = 0.125f;

        public ValueTicker( string format = "{0}" )
        {
            _format = format;
        }

        private void UpdateText( Label valueText, Label diffText )
        {
            if ( valueText != null )
            {
                valueText.Text = string.Format(_format, _lastValue - _spareValue);
            }

            if ( diffText != null )
            {
                diffText.Text = $"{(_diffSign > 0 ? "+" : "-")}{string.Format(_format, Math.Abs(_spareValue))}";
            }
        }

        public void Tick( int value, Label valueText, Label diffText, bool ignoreDecrease )
        {
            if (_firstUpdate)
            {
                _firstUpdate = false;

                if ( valueText != null )
                {
                    valueText.Text = string.Format(_format, value);
                }
            }

            if (value != _lastValue)
            {
                var diff = value - _lastValue;
                _lastValue = value;

                if (diff > 0 && ImmediateIncrease)
                {
                    _changedTime = 0f;
                    _spareValue = 1;
                    _diffSign = 1;
                }
                else
                {
                    diffText?.SetClass("negative", diff < 0);

                    if (diff > 0 || !ignoreDecrease)
                    {
                        diffText?.Style.Set("opacity",
                            $"{Math.Min(MinDiffOpacity + (1f - MinDiffOpacity) * Math.Abs(diff) / MaxDiffOpacityValue, 1.0f):F2}");
                    }
                    else
                    {
                        diffText?.Style.Set("opacity", "0.0");
                        _spareValue = 0;
                    }

                    _spareValue += diff;
                    _diffSign = Math.Sign(_spareValue);

                    _changedTime = -1f;

                    UpdateText(valueText, diffText);
                }
            }

            if (_changedTime >= 0f && _spareValue != 0)
            {
                _changedTime = -0.5f / Math.Abs(_spareValue);

                _spareValue -= Math.Sign(_spareValue) * Math.Max(MinCoinAccumulateRate, Math.Abs(_spareValue) / 20);

                if ( SoundSource != null )
                {
                    Sound.FromEntity( "click.tiny", SoundSource );
                }

                if (Math.Abs(_spareValue) < MinCoinAccumulateRate)
                {
                    _spareValue = 0;
                }

                UpdateText(valueText, diffText);
            }

            if (_changedTime > 0.5f)
            {
                diffText?.Style.Set("opacity", "0.0");
            }

        }
    }

}
