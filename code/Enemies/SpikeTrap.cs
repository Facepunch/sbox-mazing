using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies
{
    [ThreatValue(1, 2, false), UnlockLevel(14)]
    public partial class SpikeTrap : Enemy
    {
        public new const float KillRange = 32f;
        
        public const float StabStart = 0.9f;
        public const float StabEnd = 2.25f;

        [Net]
        public float StabPhase { get; set; }

        private TimeSince _lastStab;
        private float _lastTimeSinceStab;

        private TimeSince _clientLastStab;
        private float _clientLastTimeSinceStab;
        
        private bool _firstClientTick = true;

        public override float MoveSpeed => _lastStab > StabStart && _lastStab < StabEnd ? 200f : 10f;

        protected override string ModelPath => "models/spiketrap.vmdl";

        protected override PawnAnimator OnCreateAnimator() => null;
        protected override PawnController OnCreateController() => null;

        public override void Spawn()
        {
            base.Spawn();

            const float boundsWidth = 32f;

            SetupPhysicsFromAABB( PhysicsMotionType.Static, new Vector3( -boundsWidth * 0.5f, -boundsWidth * 0.5f, 0f ),
                new Vector3(boundsWidth * 0.5f, boundsWidth * 0.5f, 64f ) );

            Tags.Add( "trap" );
            Tags.Remove( "enemy" );

            EnableAllCollisions = false;

            StabPhase = Rand.Int(0, 1) * CurrentSequence.Duration * 0.5f;

            CurrentSequence.Time = StabPhase;

            _lastStab = StabPhase;
        }

        protected override void OnPostSpawn()
        {
            EyeRotation = Rotation = Rotation.FromYaw(0f);
        }

        protected override void OnServerTick()
        {
            if ( AwakeTime < 0f ) return;
            
            if ( _lastStab >= CurrentSequence.Duration )
            {
                _lastStab -= CurrentSequence.Duration;
            }

            if (_lastStab >= StabEnd && _lastTimeSinceStab < StabEnd)
            {
                EnableAllCollisions = false;
            }

            if (_lastStab >= StabStart && _lastStab < StabEnd )
            {
                var closestPlayer = Game.GetClosestPlayer(Position, KillRange, ignoreZ: false);

                if (closestPlayer != null && !closestPlayer.IsVaulting)
                {
                    Sound.FromEntity("spiketrap.hitplayer", this);

                    closestPlayer.Kill(Vector3.Up, DeathMessage, this);
                }

                EnableAllCollisions = true;
            }

            _lastTimeSinceStab = _lastStab;
        }

        protected override void OnSequenceFinished( bool looped )
        {
            base.OnSequenceFinished( looped );

            // Apparently only on client, but let's be safe
            if ( !Host.IsClient )
            {
                return;
            }

            _clientLastStab = 0f;
            Sound.FromEntity("spiketrap.initiate", this);
        }

        protected override void OnClientTick()
        {
            if (AwakeTime < 0f) return;

            if ( _firstClientTick && StabPhase != 0f )
            {
                _firstClientTick = false;
                CurrentSequence.Time = StabPhase;
            }

            if (_clientLastStab >= StabStart && _clientLastTimeSinceStab < StabStart)
            {
                Sound.FromEntity("spiketrap.trigger", this);
            }
            
            if (_clientLastStab >= StabEnd && _clientLastTimeSinceStab < StabEnd)
            {
                Sound.FromEntity("spiketrap.retract", this);
            }

            _clientLastTimeSinceStab = _clientLastStab;
        }
    }
}
