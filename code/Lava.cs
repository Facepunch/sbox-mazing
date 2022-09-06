using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Enemies;
using Sandbox;

namespace Mazing;

public class Lava : ModelEntity
{
    public const float MoveSpeed = 24f;
    public const float KillMargin = 6f;
    public const float EnemyKillMargin = 96f;

    private const float MinLightBrightness = 1f;
    private const float MaxLightBrightness = 3f;

    private PointLightEntity[] _clientLights;
    
    public override void Spawn()
    {
        SetModel("models/lava.vmdl");
        
        base.Spawn();
    }

    public override void ClientSpawn()
    {
        _clientLights = new PointLightEntity[4];

        for (var i = 0; i < 4; ++i)
        {
            var light = new PointLightEntity
            {
                Color = Color.FromRgb(0xffb600),
                Range = 192f,
                Brightness = 1f
            };

            _clientLights[i] = light;

            var pos = (i - 1.5f) * 96f;

            light.Parent = this;
            light.LocalPosition = Vector3.Forward * pos + Vector3.Up * 32f + Vector3.Right * 64f;
        }

        var spurtParticles = Particles.Create("particles/lava_spurt.vpcf", this, true);

        base.ClientSpawn();
    }
    
    private static string GetDeathMessage()
    {
        return MazingGame.GetRandomWeighted(
            ("{0} was too slow", 10f),
            ("{0} was roasted alive", 2f),
            ("{0} was burnt to a crisp", 1f),
            ("{0} was sacrificed to the volcano god", 0.01f));
    }

    private readonly List<Enemy> _enemies = new List<Enemy>();

    public void ServerTick()
    {
        Position += Vector3.Left * Time.Delta * MoveSpeed;

        foreach (var player in MazingGame.Current.PlayersAliveInMaze)
        {
            if (player.Position.y < Position.y - KillMargin)
            {
                player.Kill(Vector3.Left + Vector3.Up, GetDeathMessage(), this);
            }
        }

        _enemies.Clear();
        _enemies.AddRange(MazingGame.Current.Enemies);

        foreach (var enemy in _enemies)
        {
            if (enemy.Position.y < Position.y - EnemyKillMargin)
            {
                enemy.Kill(Vector3.Left + Vector3.Up, false);
            }
        }
    }

    [Event.Frame]
    private void ClientFrame()
    {
        if (_clientLights != null)
        {
            foreach (var light in _clientLights)
            {
                var targetBrightness = Rand.Float(MinLightBrightness, MaxLightBrightness);

                light.Brightness += (targetBrightness - light.Brightness) * 0.125f;
            }
        }
    }
}
