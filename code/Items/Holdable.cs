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
    void OnThrown( GridCoord target );
}

public abstract partial class Holdable : AnimatedEntity, IHoldable
{
    public bool IsHeld => Parent is MazingPlayer;

    protected MazingGame Game => MazingGame.Current;

    public Vector3 TargetPosition { get; set; }

    private bool _firstTick;
    private bool _hadParent;

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
        TargetPosition = Vector3.Up * 64f + Vector3.Forward * 8f;
        
        Sound.FromEntity("key.collect", this);
    }

    public void OnThrown( GridCoord target )
    {
        Parent = null;
        TargetPosition = MazingGame.Current.CellCenterToPosition( target );
    }

    [Event.Tick.Server]
    public void ServerTick()
    {
        if (_firstTick)
        {
            _firstTick = false;
            TargetPosition = LocalPosition.WithZ(0f);
        }

        LocalPosition += (TargetPosition - LocalPosition) * 0.125f;

        if (!IsHeld && _hadParent)
        {
            Sound.FromEntity("key.drop", this);
        }

        _hadParent = IsHeld;

        // Don't tick if moving to target position
        if ((TargetPosition - LocalPosition).LengthSquared > 4f * 4f)
        {
            return;
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