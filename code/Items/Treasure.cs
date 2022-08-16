using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Player;
using Sandbox;

namespace Mazing.Items;

public enum TreasureKind
{
    Emerald = 1,
    Sapphire = 2,
    Ruby = 3
}

public partial class Treasure : AnimatedEntity
{
    public const float FadeOutStartTime = 0.5f;
    public const float FaceOutDuration = 0.25f;

    private TreasureKind _kind;
    private PointLightEntity _light;

    [Net]
    public TreasureKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;

            if (_light != null)
            {
                _light.Color = GetColor(value);
            }

            SetBodyGroup(0, (int)Kind);
        }
    }

    public static int GetValue( TreasureKind kind )
    {
        return kind switch
        {
            TreasureKind.Emerald => 5,
            TreasureKind.Sapphire => 20,
            TreasureKind.Ruby => 100,
            _ => 1
        };
    }

    public static Color GetColor( TreasureKind kind )
    {
        return kind switch
        {
            TreasureKind.Emerald => Color.FromRgb(0x32cd32),
            TreasureKind.Sapphire => Color.FromRgb(0x3150cd),
            TreasureKind.Ruby => Color.FromRgb(0x8b0000),
            _ => Color.FromRgb(0xf2d873)
        };
    }

    public static string GetSound( TreasureKind kind )
    {
        return kind switch
        {
            TreasureKind.Emerald => "gem1.collect",
            TreasureKind.Sapphire => "gem2.collect",
            TreasureKind.Ruby => "gem3.collect",
            _ => "gem1.collect"
        };
    }

    public int Value => GetValue(Kind);

    public bool IsCollected { get; private set; }

    private TimeSince _collectedTime;

    public Treasure()
    {

    }

    public Treasure( TreasureKind kind )
    {
        Kind = kind;
    }

    public override void Spawn()
    {
        base.Spawn();

        SetModel("models/item.vmdl");

        Tags.Add("treasure");

        if (IsServer)
        {
            _light = new PointLightEntity()
            {
                Color = GetColor(0),
                Brightness = 0.5f,
                Range = 48f
            };

            _light.SetParent(this, "Item", Transform.Zero);
        }

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }

    public void ServerTick()
    {
        if (IsCollected)
        {
            LocalPosition = LocalPosition.WithZ(LocalPosition.z + (192f - LocalPosition.z) * 0.25f);

            _light.Brightness = Math.Max(0.75f - _collectedTime / FadeOutStartTime, 0f);

            if (_collectedTime > FadeOutStartTime)
            {
                var alpha = 1f - (_collectedTime - FadeOutStartTime) / FaceOutDuration;

                RenderColor = RenderColor.WithAlpha(Math.Clamp(alpha, 0f, 1f));

                if (alpha < 0f)
                {
                    Delete();
                }
            }

            return;
        }

        var game = MazingGame.Current;

        var player = game.GetClosestPlayer(Position, 24f, true, false);

        if (player != null)
        {
            Collect(player);
        }
    }

    public void Collect( MazingPlayer player )
    {
        if (IsCollected) return;

        IsCollected = true;
        Parent = player;
        LocalPosition = Vector3.Zero;

        Sound.FromEntity(GetSound(Kind), this);

        _collectedTime = 0f;

        player.AddCoins(Value);
    }
}
