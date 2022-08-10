using Sandbox;

namespace Mazing.Enemies;

[EnemySpawn(FirstLevel = 3)]
internal partial class Wizard : Enemy
{
    public override float MoveSpeed => 20f;

    private float _teleportTimer;
    private const float TELEPORT_DELAY = 4f;

    public override void Spawn()
    {
        base.Spawn();

        //new ModelEntity("models/citizen_clothes/hat/hat.tophat.vmdl", this);
        new ModelEntity("models/citizen_clothes/dress/posh_dress/Models/posh_dress.vmdl", this);
        new ModelEntity("models/citizen_clothes/gloves/long_white_gloves/Models/long_white_gloves.vmdl", this);

        _teleportTimer = TELEPORT_DELAY;
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        _teleportTimer -= Time.Delta;
        if (_teleportTimer <= 0f)
        {
            var newCell = Game.GetRandomCell();
            Position = Game.CellCenterToPosition(newCell);
            TargetCell = newCell;

            _teleportTimer = TELEPORT_DELAY;
        }

        DebugOverlay.Text("Wizard", EyePosition, 0f, float.MaxValue);
    }

    protected override void OnReachTarget()
    {
        
    }
}