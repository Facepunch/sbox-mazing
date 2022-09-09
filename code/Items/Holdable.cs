using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mazing.Player;
using Sandbox;

namespace Mazing.Items;

public interface IHoldable
{
    Entity Parent { get; set; }

    void OnPickedUp( MazingPlayer holder );
    void OnThrown( GridCoord target, Direction direction );

    bool IsHeavy { get; }
}

public abstract partial class Holdable : AnimatedEntity, IHoldable
{
    public const float ThrowSpeed = 192f;

    public bool IsHeld => Parent is MazingPlayer;

    protected MazingGame Game => MazingGame.Current;

    public Vector3 TargetPosition { get; private set; }

    private Vector3 _startPosition;
    private TimeSince _throwTime;

    private bool _firstTick;
    private bool _hadParent;

    public virtual bool IsHeavy => false;

    [Net]
    public MazingPlayer LastHolder { get; set; }

    public override void Spawn()
    {
        base.Spawn();

        _firstTick = true;

        EnableDrawing = true;
        EnableSolidCollisions = true;
    }

    public void OnPickedUp( MazingPlayer holder )
    {
        Parent = holder;
        LastHolder = holder;

        _throwTime = float.PositiveInfinity;
        TargetPosition = Vector3.Up * 64f + Vector3.Forward * 8f;
        
        Sound.FromEntity("key.collect", this);
    }

    public void OnThrown( GridCoord target, Direction direction )
    {
        Parent = null;

        _throwTime = 0f;

        _startPosition = LocalPosition;
        TargetPosition = MazingGame.Current.CellCenterToPosition( target );
    }

    [Event.Tick.Server]
    public void ServerTick()
    {
        if (!IsHeld && _hadParent)
        {
            Sound.FromEntity("key.drop", this);
        }

        _hadParent = IsHeld;

        if (!IsHeld && Parent != null)
        {
            OnServerTick();
            return;
        }

        if (_firstTick)
        {
            _firstTick = false;
            _startPosition = TargetPosition = LocalPosition.WithZ(0f);
            _throwTime = float.PositiveInfinity;
        }

        var travelDist = (TargetPosition - _startPosition).WithZ(0f);
        var travelTime = travelDist.Length / ThrowSpeed;

        if (_throwTime >= 0f && _throwTime <= travelTime)
        {
            var t = _throwTime/ travelTime;

            LocalPosition = Vector3.Lerp(_startPosition, TargetPosition, t)
                + Vector3.Up * (1f - (2f * t - 1f) * (2f * t - 1f)) * 192f;

            // Don't tick if being thrown
            return;
        }
        else
        {
            LocalPosition += (TargetPosition - LocalPosition) * 0.125f;
        }
        
        OnServerTick();
    }

    protected virtual void OnServerTick()
    {
        if (IsHeld)
        {
            return;
        }

        var closestPlayer = Game.GetClosestPlayer(Position, 20f);

        if (closestPlayer != null && closestPlayer.CanPickUpItem)
        {
            closestPlayer.PickUp( this );
        }
    }
}