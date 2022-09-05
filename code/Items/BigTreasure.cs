using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Items;

public partial class BigTreasure : Holdable
{
    public const float FadeOutStartTime = 0.5f;
    public const float FadeOutDuration = 0.25f;

    public bool IsDelivered { get; set; }

    private PointLightEntity _light;
    private TimeSince _collectedTime;

    public override bool IsHeavy => true;

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/item.vmdl");
        SetBodyGroup(0, 4);

        Tags.Add("treasure");

        if (IsServer)
        {
            _light = new PointLightEntity()
            {
                Color = Treasure.GetColor(TreasureKind.Diamond),
                Brightness = 1f,
                Range = 64f
            };

            _light.SetParent(this, "Item", Transform.Zero);
        }
    }

    protected override void OnServerTick()
    {
        if (IsDelivered)
        {
            _light.Brightness = Math.Max(0.75f - _collectedTime / FadeOutStartTime, 0f);

            if (_collectedTime > FadeOutStartTime)
            {
                var alpha = 1f - (_collectedTime - FadeOutStartTime) / FadeOutDuration;

                RenderColor = RenderColor.WithAlpha(Math.Clamp(alpha, 0f, 1f));

                if (alpha < 0f)
                {
                    Delete();
                }
            }

            return;
        }

        base.OnServerTick();

        var hatch = MazingGame.Current.Hatch;

        if (hatch == null || !hatch.IsOpen)
        {
            return;
        }

        var diff = hatch.Position.WithZ(0) - Position.WithZ(0);

        if (diff.LengthSquared < 16f * 16f)
        {
            IsDelivered = true;

            Parent = hatch;
            LocalPosition = Vector3.Zero;

            Sound.FromEntity(Treasure.GetSound(TreasureKind.Diamond), this);

            CurrentSequence.Name = "collect";

            _collectedTime = 0f;

            Game.TotalCoins += Treasure.GetValue(TreasureKind.Diamond);
        }
    }
}