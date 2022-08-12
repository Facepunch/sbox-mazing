using System;
using System.Linq;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(5), ThreatValue(2)]
internal partial class Wizard : Enemy
{
    public override float MoveSpeed => 20f;

    protected override int HoldType => FiredBolt && !IsTeleporting && _firedBoltTime > 0.15f ? (_firedBoltTime > 2.75f ? _teleportHoldType : 0) : 5;
    private int _teleportHoldType;

    public bool IsTeleporting { get; set; } = false;
    public bool FiredBolt { get; set; }

    private TimeSince _firedBoltTime;

    private GridCoord _teleportCell;

    private TimeSince _teleportTimer;
    private const float TELEPORT_DELAY = 4f;
    private const float TELEPORT_DISAPPEAR_TIME = 3f;
    private const float FIRE_BOLT_DELAY = 1f;

    private Particles _spawnParticles;
    private Particles _popParticles;

    private bool _playedPortalSound;

    public override void Spawn()
    {
        base.Spawn();

        Clothing = new ClothingContainer();
        AddClothingItem("models/citizen_clothes/skin05.clothing");
        AddClothingItem("models/citizen_clothes/dress/Skirt/skirt.clothing");
        AddClothingItem("models/citizen_clothes/necklace/necklace/necklace.clothing");
        AddClothingItem("models/citizen_clothes/hair/hair_balding/hair_baldinggrey.clothing");
        AddClothingItem("models/citizen_clothes/hair/scruffy_beard/scruffy_beard_grey.clothing");
        Clothing.DressEntity(this);

        RenderColor = new Color(0.75f, 0f, 0.75f);

        //_teleportHoldType = Rand.Float(0f, 1f) < 0.5f ? 1 : 3;
        _teleportHoldType = 3;

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
            if ( !_playedPortalSound )
            {
                _playedPortalSound = true;

                Sound.FromWorld("wizard.portal", Position);
            }

            if (_teleportTimer >= TELEPORT_DISAPPEAR_TIME)
            {
                Show();
                EnableAllCollisions = true;

                Sound.FromWorld( "wizard.appear", Position );

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

                _firedBoltTime = 0f;

                var bolt = new WizardBolt
                {
                    Direction = this.GetFacingDirection(),
                    Position = Position + Vector3.Up * 48f
                };
                
                Sound.FromEntity( "wizard.shoot", this );
            }

            if (_teleportTimer >= TELEPORT_DELAY)
            {
                IsTeleporting = true;
                
                _spawnParticles?.Destroy();
                _spawnParticles = null;

                _popParticles?.Destroy();
                _popParticles = Particles.Create("particles/wizard_spawn_end.vpcf", Position);

                _teleportCell = Game.GetRandomEmptyCell();

                Sound.FromWorld( "wizard.disappear", Position );

                Hide();
                EnableAllCollisions = false;

                Position = Game.CellCenterToPosition(_teleportCell);
                TargetCell = _teleportCell;

                _spawnParticles = Particles.Create( "particles/wizard_spawn.vpcf", Game.CellCenterToPosition( _teleportCell ) );

                _playedPortalSound = false;

                _teleportTimer = 0f;

                //_teleportHoldType = Rand.Float(0f, 1f) < 0.5f ? 1 : 3;
                _teleportHoldType = 3;
            }
        }

        //DebugOverlay.Text($"FiredBolt: {FiredBolt}", EyePosition, 0f, float.MaxValue);
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
            Sound.FromEntity("wizard.boltkill", this);
            player.Kill( ((GridCoord)Direction).Normal, "{0} was zapped by a Wizard" );
        }

        if ( this.GetCellIndex() != cell )
        {
            if ( game.CurrentMaze.GetWall( cell, Direction ) )
            {
                RenderColor = Color.Transparent;

                _particles?.Destroy();
                _particles = null;

                Sound.FromEntity( "wizard.bolthitwall", this );

                _isDespawning = true;
                _despawnTime = 0f;
            }
        }
    }
}