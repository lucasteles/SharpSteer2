// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.LowSpeedTurn;

class LowSpeedTurnPlugIn : PlugIn
{
    const int LstCount = 5;
    const float LstLookDownDistance = 18;
    static readonly Vector3 lstViewCenter = new(7, 0, -2);
    static readonly Vector3 lstPlusZ = new(0, 0, 1);

    public LowSpeedTurnPlugIn(IAnnotationService annotations)
        :base(annotations) =>
        all = new();

    public override string Name => "Low Speed Turn";

    public override float SelectionOrderSortKey => 0.05f;

    public override void Open()
    {
        // create a given number of agents with stepped inital parameters,
        // store pointers to them in an array.
        LowSpeedTurn.ResetStarts();
        for (var i = 0; i < LstCount; i++)
            all.Add(new(annotations));

        // initial selected vehicle
        GameDemo.SelectedVehicle = all[0];

        // initialize camera
        GameDemo.Camera.Mode = Camera.CameraMode.Fixed;
        GameDemo.Camera.FixedUp = lstPlusZ;
        GameDemo.Camera.FixedTarget = lstViewCenter;
        GameDemo.Camera.FixedPosition = lstViewCenter;
        GameDemo.Camera.FixedPosition.Y += LstLookDownDistance;
        GameDemo.Camera.LookDownDistance = LstLookDownDistance;
        GameDemo.Camera.FixedDistanceVerticalOffset = GameDemo.Camera2DElevation;
        GameDemo.Camera.FixedDistanceDistance = GameDemo.CameraTargetDistance;
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        // update, draw and annotate each agent
        foreach (var t in all)
            t.Update(currentTime, elapsedTime);
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // selected vehicle (user can mouse click to select another)
        var selected = GameDemo.SelectedVehicle;

        // vehicle nearest mouse (to be highlighted)
        var nearMouse = GameDemo.VehicleNearestToMouse();

        // update camera
        GameDemo.UpdateCamera(elapsedTime, selected);

        // draw "ground plane"
        GameDemo.GridUtility(selected.Position);

        // update, draw and annotate each agent
        foreach (var agent in all)
        {
            agent.Draw();

            // display speed near agent's screen position
            var textColor = new Color(new Vector3(0.8f, 0.8f, 1.0f).ToXna());
            var textOffset = new Vector3(0, 0.25f, 0);
            var textPosition = agent.Position + textOffset;
            var annote = $"{agent.Speed:0.00}";
            Drawing.Draw2dTextAt3dLocation(annote, textPosition, textColor);
        }

        // highlight vehicle nearest mouse
        GameDemo.HighlightVehicleUtility(nearMouse);
    }

    public override void Close() => all.Clear();

    public override void Reset()
    {
        // reset each agent
        LowSpeedTurn.ResetStarts();
        foreach (var t in all)
            t.Reset();
    }

    public override IEnumerable<IVehicle> Vehicles => all.ConvertAll<IVehicle>(v => (IVehicle) v);

    readonly List<LowSpeedTurn> all; // for allVehicles
}