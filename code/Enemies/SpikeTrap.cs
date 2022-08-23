﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies
{
    [ThreatValue(1), UnlockLevel(14)]
    public partial class SpikeTrap : Enemy
    {
        public new const float KillRange = 24f;

        public const float StabPeriod = 4f;
        public const float StabStart = 0.9f;
        public const float StabEnd = 2f;

        private TimeSince _lastStab;

        public override float MoveSpeed => _lastStab > StabStart && _lastStab < StabEnd ? 200f : 10f;

        protected override string ModelPath => "models/spiketrap.vmdl";

        protected override PawnAnimator OnCreateAnimator() => null;
        protected override PawnController OnCreateController() => null;

        public override void Spawn()
        {
            base.Spawn();

            PlaybackRate = 0f;
            CurrentSequence.Time = 0f;
        }

        protected override void OnSequenceFinished( bool looped )
        {
            base.OnSequenceFinished( looped );

            _lastStab = 0f;
        }

        protected override void OnServerTick()
        {
            if ( AwakeTime < 0f ) return;

            if ( _lastStab > StabPeriod )
            {
                _lastStab = 0f;

                CurrentSequence.Time = 0f;
                PlaybackRate = 1f;

                // TODO: stab sound
            }

            if (_lastStab > StabStart && _lastStab < StabEnd )
            {
                var closestPlayer = Game.GetClosestPlayer(Position, KillRange, ignoreZ: false);
                
                if (closestPlayer != null && !closestPlayer.IsVaulting)
                {
                    // TODO: player stabbed sound
                    // Sound.FromEntity("enemy.punch", this);

                    closestPlayer.Kill( Vector3.Up, DeathMessage );
                }
            }
        }
    }
}