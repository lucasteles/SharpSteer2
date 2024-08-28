// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Collections.Generic;

namespace SharpSteer2.Demo.PlugIns.MultiplePursuit;

public class MpPlugIn : PlugIn
{
    public MpPlugIn(IAnnotationService annotations)
        :base(annotations) =>
        allMp = new();

    public override string Name => "Multiple Pursuit";

    public override float SelectionOrderSortKey => 0.04f;

    public override void Open()
    {
        // create the wanderer, saving a pointer to it
        wanderer = new(annotations);
        allMp.Add(wanderer);

        // create the specified number of pursuers, save pointers to them
        const int pursuerCount = 30;
        for (var i = 0; i < pursuerCount; i++)
            allMp.Add(new MpPursuer(wanderer, annotations));
        //pBegin = allMP.begin() + 1;  // iterator pointing to first pursuer
        //pEnd = allMP.end();          // iterator pointing to last pursuer

        // initialize camera
        GameDemo.SelectedVehicle = wanderer;
        GameDemo.Camera.Mode = Camera.CameraMode.StraightDown;
        GameDemo.Camera.FixedDistanceDistance = GameDemo.CameraTargetDistance;
        GameDemo.Camera.FixedDistanceVerticalOffset = GameDemo.Camera2DElevation;
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        // update the wanderer
        wanderer.Update(currentTime, elapsedTime);

        // update each pursuer
        for (var i = 1; i < allMp.Count; i++)
        {
            ((MpPursuer)allMp[i]).Update(currentTime, elapsedTime);
        }
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

        // draw each vehicles
        foreach (var mp in allMp)
            mp.Draw();

        // highlight vehicle nearest mouse
        GameDemo.HighlightVehicleUtility(nearMouse);
        GameDemo.CircleHighlightVehicleUtility(selected);
    }

    public override void Close() =>
        // delete wanderer, all pursuers, and clear list
        allMp.Clear();

    public override void Reset()
    {
        // reset wanderer and pursuers
        wanderer.Reset();
        for (var i = 1; i < allMp.Count; i++) allMp[i].Reset();

        // immediately jump to default camera position
        GameDemo.Camera.DoNotSmoothNextMove();
        GameDemo.Camera.ResetLocalSpace();
    }

    //const AVGroup& allVehicles () {return (const AVGroup&) allMP;}
    public override IEnumerable<IVehicle> Vehicles => allMp.ConvertAll<IVehicle>(m => (IVehicle) m);

    // a group (STL vector) of all vehicles
    readonly List<MpBase> allMp;

    MpWanderer wanderer;
}