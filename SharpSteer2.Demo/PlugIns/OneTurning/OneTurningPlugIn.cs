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

namespace SharpSteer2.Demo.PlugIns.OneTurning;

public class OneTurningPlugIn : PlugIn
{
    public OneTurningPlugIn(IAnnotationService annotations)
        :base(annotations) =>
        theVehicle = new();

    public override string Name => "One Turning Away";

    public override float SelectionOrderSortKey => 0.06f;

    public override void Open()
    {
        oneTurning = new(annotations);
        GameDemo.SelectedVehicle = oneTurning;
        theVehicle.Add(oneTurning);

        // initialize camera
        GameDemo.Init2dCamera(oneTurning);
        GameDemo.Camera.Position = new(10, GameDemo.Camera2DElevation, 10);
        GameDemo.Camera.FixedPosition = new(40);
    }

    public override void Update(float currentTime, float elapsedTime) =>
        // update simulation of test vehicle
        oneTurning.Update(currentTime, elapsedTime);

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // draw "ground plane"
        GameDemo.GridUtility(oneTurning.Position);

        // draw test vehicle
        oneTurning.Draw();

        // textual annotation (following the test vehicle's screen position)
        var annote = $"      speed: {oneTurning.Speed:0.00}";
        Drawing.Draw2dTextAt3dLocation(annote, oneTurning.Position, Color.Red);
        Drawing.Draw2dTextAt3dLocation("start", Vector3.Zero, Color.Green);

        // update camera, tracking test vehicle
        GameDemo.UpdateCamera(elapsedTime, oneTurning);
    }

    public override void Close()
    {
        theVehicle.Clear();
        oneTurning = null;
    }

    public override void Reset() =>
        // reset vehicle
        oneTurning.Reset();

    public override IEnumerable<IVehicle> Vehicles => theVehicle.ConvertAll<IVehicle>(v => (IVehicle) v);

    OneTurning oneTurning;
    readonly List<OneTurning> theVehicle; // for allVehicles
}