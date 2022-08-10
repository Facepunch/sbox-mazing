using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 0)]
internal partial class Wizard : Enemy
{
    public override float MoveSpeed => 20f;

    public bool IsTeleporting { get; set; } = false;
    private GridCoord _teleportCell;

    private TimeSince _teleportTimer;
    private const float TELEPORT_DELAY = 4f;
    private const float TELEPORT_DISAPPEAR_TIME = 1.5f;

    private Particles _spawnParticles;

    public override void Spawn()
    {
        base.Spawn();

        //new ModelEntity("models/citizen_clothes/hat/hat.tophat.vmdl", this);
        new ModelEntity("models/citizen_clothes/dress/Office_Skirt/Models/office_skirt.vmdl", this);
        //new ModelEntity("models/citizen_clothes/gloves/long_white_gloves/Models/long_white_gloves.vmdl", this);
        RenderColor = new Color(0.7f, 0f, 0.7f);

        _teleportTimer = 0f;
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();

        _spawnParticles?.Destroy();
        _spawnParticles = null;
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        if (IsTeleporting)
        {
            if (_teleportTimer >= TELEPORT_DELAY)
            {
                Position = Game.CellCenterToPosition(_teleportCell);
                TargetCell = _teleportCell;

                IsTeleporting = false;
                _teleportTimer = 0f;

                if (_spawnParticles != null)
                {
                    _spawnParticles.SetPosition(1, Position + Vector3.Up * 32f);
                    _spawnParticles.Destroy();
                    _spawnParticles = null;
                }
            }
        }
        else
        {
            if (_teleportTimer >= TELEPORT_DISAPPEAR_TIME)
            {
                IsTeleporting = true;

                if (_spawnParticles != null)
                {
                    _spawnParticles.Destroy();
                    _spawnParticles = null;
                }

                _teleportCell = Game.GetRandomCell();
                Position = new Vector3(0f, 0f, -666f);

                _spawnParticles = Particles.Create( "particles/wizard_spawn.vpcf", Game.CellCenterToPosition( _teleportCell ) );
                _spawnParticles.SetPosition(1, Vector3.Forward * -4096f);

                _teleportTimer = 0f;
            }
        }

        //DebugOverlay.Text($"_teleportTimer: {_teleportTimer}", new Vector3(0f, 0f, 0f), 0f, float.MaxValue);
    }

    protected override void OnReachTarget()
    {
        
    }
}