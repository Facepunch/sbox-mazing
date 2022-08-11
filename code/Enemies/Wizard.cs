using System;
using System.Linq;
using Sandbox;

namespace Mazing.Enemies;

internal partial class Wizard : Enemy
{
    public override float MoveSpeed => 20f;

    public bool IsTeleporting { get; set; } = false;
    public bool FiredBolt { get; set; }

    private GridCoord _teleportCell;

    private TimeSince _teleportTimer;
    private const float TELEPORT_DELAY = 4f;
    private const float TELEPORT_DISAPPEAR_TIME = 3f;
    private const float FIRE_BOLT_DELAY = 1f;

    private Particles _spawnParticles;
    private Particles _popParticles;

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

        _spawnParticles?.Destroy( true );
        _spawnParticles = null;

        _popParticles?.Destroy( true );
        _popParticles = null;
    }

    private readonly int[] _dirDistances = new int[4];

    protected override void OnLevelChange()
    {
        base.OnLevelChange();

        Log.Info( "OnLevelChange" );

        _spawnParticles?.Destroy();
        _spawnParticles = null;

        _popParticles?.Destroy();
        _popParticles = null;

        _teleportTimer = -Rand.Float( 0f, TELEPORT_DELAY ) + TELEPORT_DISAPPEAR_TIME;
        IsTeleporting = true;

        _teleportCell = this.GetCellIndex();
        _spawnParticles = Particles.Create("particles/wizard_spawn.vpcf", Game.CellCenterToPosition(_teleportCell));

        Hide();
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();

        if ( AwakeTime < 0f )
        {
            return;
        }

        if (IsTeleporting)
        {
            if (_teleportTimer >= TELEPORT_DISAPPEAR_TIME)
            {
                Show();
                EnableAllCollisions = true;

                var cell = this.GetCellIndex();
                var totalDist = 0;

                foreach ( var (dir, delta) in MazeData.Directions )
                {
                    var wallCell = Game.CurrentMaze.RayCast(cell, dir );
                    var dist = (wallCell - cell).Distance;

                    _dirDistances[(int)dir] = dist;
                    totalDist += dist;
                }

                var targetVal = Rand.Int( 0, totalDist - 1 );

                foreach ( var (dir, delta) in MazeData.Directions )
                {
                    targetVal -= _dirDistances[(int)dir];

                    if ( targetVal < 0 )
                    {
                        EyeRotation = Rotation = Rotation.LookAt( delta.Normal, Vector3.Up );
                        break;
                    }
                }
                
                IsTeleporting = false;
                FiredBolt = false;

                _teleportTimer = 0f;
                
                _spawnParticles?.Destroy();
                _spawnParticles = null;

                _popParticles?.Destroy();
                _popParticles = Particles.Create("particles/wizard_spawn_end.vpcf", Position);
            }
        }
        else
        {
            if ( _teleportTimer >= FIRE_BOLT_DELAY && !FiredBolt )
            {
                FiredBolt = true;

                Animator.Trigger("b_attack");

                var bolt = new WizardBolt
                {
                    Direction = this.GetFacingDirection(),
                    Position = Position + Vector3.Up * 48f
                };
            }

            if (_teleportTimer >= TELEPORT_DELAY)
            {
                IsTeleporting = true;
                
                _spawnParticles?.Destroy();
                _spawnParticles = null;

                _popParticles?.Destroy();
                _popParticles = Particles.Create("particles/wizard_spawn_end.vpcf", Position);

                _teleportCell = Game.GetRandomEmptyCell();

                Hide();
                EnableAllCollisions = false;

                Position = Game.CellCenterToPosition(_teleportCell);
                TargetCell = _teleportCell;

                _spawnParticles = Particles.Create( "particles/wizard_spawn.vpcf", Game.CellCenterToPosition( _teleportCell ) );

                _teleportTimer = 0f;
            }
        }

        //DebugOverlay.Text($"_teleportTimer: {_teleportTimer}", new Vector3(0f, 0f, 0f), 0f, float.MaxValue);
    }

    protected override void OnReachTarget()
    {
        
    }
}

partial class WizardBolt : ModelEntity
{
    public float KillRange { get; } = 16f;

    public float MoveSpeed { get; } = 160f;

    [Net]
    public Direction Direction { get; set; }

    private bool _isDespawning;
    private PointLightEntity _light;
    private TimeSince _despawnTime;

    private Particles _particles;

    public override void Spawn()
    {
        base.Spawn();

        SetModel( "models/wizard_bolt.vmdl" );

        Tags.Add( "projectile" );

        if ( IsServer )
        {
            _light = new PointLightEntity
            {
                Color = new Color32( 190, 146, 255, 255 ),
                Range = 128f,
                Brightness = 1f,
                Parent = this,
                LocalPosition = default
            };

            _particles = Particles.Create( "particles/wizard_bolt.vpcf", this );
        }

        EnableDrawing = true;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        _particles?.Destroy();
        _particles = null;
    }

    [Event.Tick.Server]
    private void ServerTick()
    {
        if (_isDespawning)
        {
            _light.Brightness = Math.Max( 1f - _despawnTime * 4f, 0f );

            if ( _despawnTime > 0.5f )
            {
                Delete();
            }

            return;
        }

        var game = MazingGame.Current;
        var cell = this.GetCellIndex();

        var dir = game.CellCenterToPosition( cell + Direction ) -
                  game.CellCenterToPosition( cell );

        Position += dir.Normal * Time.Delta * MoveSpeed;

        var player = MazingGame.Current.GetClosestPlayer( Position, KillRange );

        if ( player != null )
        {
            player.Kill( ((GridCoord)Direction).Normal );
        }

        if ( this.GetCellIndex() != cell )
        {
            if ( game.CurrentMaze.GetWall( cell, Direction ) )
            {
                RenderColor = Color.Transparent;

                _particles?.Destroy();
                _particles = null;

                _isDespawning = true;
                _despawnTime = 0f;
            }
        }
    }
}