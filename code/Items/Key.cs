using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Items;

public partial class Key : Holdable
{
    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/item.vmdl");
        SetBodyGroup(0, 0);

        Tags.Add("key");

        if (Sandbox.Game.IsServer)
        {
            var light = new PointLightEntity()
            {
                Color = Treasure.GetColor(0),
                Brightness = 1f,
                Range = 64f
            };

            light.SetParent(this, "Item", Transform.Zero);
        }
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        var hatch = MazingGame.Current.Hatch;

        if (hatch == null || hatch.IsOpen)
        {
            return;
        }

        var diff = hatch.Position.WithZ(0) - Position.WithZ(0);

        if (diff.LengthSquared < 16f * 16f)
        {
            hatch.Open();
            Delete();

            Game.Key = null;
        }
    }
}