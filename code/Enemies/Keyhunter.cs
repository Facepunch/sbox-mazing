using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;

namespace Mazing.Enemies;

partial class Keyhunter : Enemy
{
    public override float MoveSpeed => IsHuntingKey() ? 118f : 75f;

    private Color _colorNormal = new Color(0.66f, 0.66f, 0.3f);
    private Color _colorHunting = new Color(1f, 1f, 0f);

    public override void Spawn()
    {
        base.Spawn();

        //SetModel("models/citizen_mannequin/mannequin.vmdl");
        //new ModelEntity( "models/citizen_clothes/hat/hat_beret.black.vmdl", this );
        new ModelEntity("models/citizen_clothes/hair/hair_shortscruffy/Models/hair_shortscruffy_grey.vmdl", this);
        //new ModelEntity("models/citizen_clothes/glasses/Stylish_Glasses/Models/stylish_glasses_black.vmdl", this);
    }

    protected override void OnReachTarget()
    {
        if ( Game.Key?.IsHeld ?? false )
        {
            TargetCell = GetNextInPathTo( Game.Key.GetCellIndex() );
        }
        else
        {
            TargetCell = GetRandomNeighborCell();
        }
    }

    protected override void OnServerTick()
    {
        base.OnServerTick();
        RenderColor = IsHuntingKey() ? _colorHunting : _colorNormal;
    }

    public bool IsHuntingKey()
    {
        var key = Entity.All.OfType<Key>().FirstOrDefault();
        return (key == null || key.IsHeld);
    }
}