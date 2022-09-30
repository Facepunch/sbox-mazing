using System;
using System.Linq;
using Sandbox;

namespace Mazing.Enemies;

[UnlockLevel(30), ThreatValue(3), Replaces(typeof(Wizard), 15)]
internal partial class EliteWizard : Enemy
{
    public override string NounPhrase => "an Elite Wizard";
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
        AddClothingItem("models/citizen_clothes/skin01.clothing");
        AddClothingItem("models/citizen_clothes/dress/Office_Skirt/office_skirt.clothing");
        //AddClothingItem("models/citizen_clothes/vest/Tactical_Vest/Models/tactical_vest.clothing");
        //AddClothingItem("models/citizen_clothes/hair/hair_balding/hair_baldinggrey.clothing");
        //AddClothingItem("models/citizen_clothes/hair/hair_looseblonde/hair.loose.grey.clothing");
        AddClothingItem("models/citizen_clothes/hair/hair_longbrown/Models/hair_longgrey.clothing");
        AddClothingItem("models/citizen_clothes/gloves/long_white_gloves/long_white_gloves.clothing");
        AddClothingItem("models/citizen_clothes/hair/scruffy_beard/scruffy_beard_grey.clothing");
        Clothing.DressEntity(this);

        foreach (var child in Children.ToArray())
        {
            if (child is ModelEntity e && e.Tags.Has("clothes"))
            {
                e.RenderColor = new Color(0f, 0.5f, 1f, 1f);
            }
        }

        RenderColor = new Color(0f, 0f, 1f, 0.45f);

        //_teleportHoldType = Rand.Float(0f, 1f) < 0.5f ? 1 : 3;
        _teleportHoldType = 3;

        _teleportTimer = 0f;

        Scale = 1.15f;
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
        _spawnParticles = Particles.Create("particles/wizard_spawn_elite.vpcf", Game.CellCenterToPosition(_teleportCell));

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
                    var currCell = cell;
                    var dist = 0;
                    while(Game.IsInMaze(currCell))
                    {
                        currCell += delta;
                        dist++;
                    }

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
                _popParticles = Particles.Create("particles/wizard_spawn_end_elite.vpcf", Position);
            }
        }
        else
        {
            if ( _teleportTimer >= FIRE_BOLT_DELAY && !FiredBolt )
            {
                FiredBolt = true;

                Animator.Trigger("b_attack");

                _firedBoltTime = 0f;

                var bolt = new WizardBolt(true)
                {
                    Direction = this.GetFacingDirection(),
                    Position = Position + Vector3.Up * 48f,
                    Scale = 1.375f,
                };
                
                Sound.FromEntity( "wizard.shoot", this );
            }

            if (_teleportTimer >= TELEPORT_DELAY)
            {
                IsTeleporting = true;
                
                _spawnParticles?.Destroy();
                _spawnParticles = null;

                _popParticles?.Destroy();
                _popParticles = Particles.Create("particles/wizard_spawn_end_elite.vpcf", Position);

                _teleportCell = Game.GetRandomConnectedEmptyCell(this.GetCellIndex());

                Sound.FromWorld( "wizard.disappear", Position );

                Hide();
                EnableAllCollisions = false;

                Position = Game.CellCenterToPosition(_teleportCell);
                TargetCell = _teleportCell;

                _spawnParticles = Particles.Create( "particles/wizard_spawn_elite.vpcf", Game.CellCenterToPosition( _teleportCell ) );

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
