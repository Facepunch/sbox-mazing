using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.UI;

namespace Mazing.UI
{
    [UseTemplate]
    internal partial class HudRoot : RootPanel
    {
        public int LevelNumber => MazingGame.Current.LevelIndex + 1;

        public HudRoot()
        {

        }
    }
}
