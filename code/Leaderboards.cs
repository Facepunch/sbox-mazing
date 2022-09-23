using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing
{
    [Leaderboard.Type("depth")]
    public struct DepthScore
    {
        [Leaderboard.Field(Title = "Completed Levels", Format = "{0} Levels")]
        public int Depth { get; set; }

        [Leaderboard.Field(Format = @"{0:mm\:ss\.ff}")]
        public TimeSpan Time { get; set; }

        private const int MaxTime = 24 * 60 * 60 * 100;

        public int Encoded => Depth * MaxTime - Math.Clamp((int)Math.Round(Time.TotalSeconds * 100), 0, MaxTime - 1);
    }

    [Leaderboard.Type("money")]
    public struct MoneyScore
    {
        [Leaderboard.Field(Title = "Treasure Value", Format = @"${0:N0}")]
        public int Value { get; set; }
    }
}