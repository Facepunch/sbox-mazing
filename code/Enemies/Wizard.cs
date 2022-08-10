using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 5, SpawnPeriod = 3)]
internal partial class Wizard : Enemy
{
    public override float MoveSpeed => 20f;

    public bool IsTeleporting { get; set; } = false;
    private GridCoord _teleportCell;

    private float _teleportTimer;
    private const float TELEPORT_DELAY = 4f;
    private const float TELEPORT_DISAPPEAR_TIME = 1.5f;

    private PointLightEntity _light;

    public override void Spawn()
    {
        base.Spawn();

        //new ModelEntity("models/citizen_clothes/hat/hat.tophat.vmdl", this);
        new ModelEntity("models/citizen_clothes/dress/Office_Skirt/Models/office_skirt.vmdl", this);
        //new ModelEntity("models/citizen_clothes/gloves/long_white_gloves/Models/long_white_gloves.vmdl", this);
        RenderColor = new Color(0.7f, 0f, 0.7f);

        _teleportTimer = TELEPORT_DELAY;
    }


    protected override void OnDestroy()
    {
        base.OnDestroy();

        _light?.Delete();
        _light = null;
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        if (IsTeleporting)
        {
            _teleportTimer -= Time.Delta;
            if (_teleportTimer <= 0f)
            {
                if (_light != null)
                {
                    _light.Delete();
                    _light = null;
                }

                Position = Game.CellCenterToPosition(_teleportCell);
                TargetCell = _teleportCell;

                IsTeleporting = false;
                _teleportTimer = TELEPORT_DELAY;
            }
        }
        else
        {
            _teleportTimer -= Time.Delta;
            if (_teleportTimer <= 0f)
            {
                IsTeleporting = true;

                if (_light != null)
                {
                    _light.Delete();
                    _light = null;
                }

                _teleportCell = Game.GetRandomCell();
                Position = new Vector3(-666f, -666f, -666f);

                _light = new PointLightEntity
                {
                    Position = Game.CellCenterToPosition(_teleportCell),
                    Color = Color.FromRgb(0x880088),
                    Range = 128f
                };

                _teleportTimer = TELEPORT_DISAPPEAR_TIME;
            }
        }

        //DebugOverlay.Text($"_teleportTimer: {_teleportTimer}", new Vector3(0f, 0f, 0f), 0f, float.MaxValue);
    }

    protected override void OnReachTarget()
    {
        
    }
}