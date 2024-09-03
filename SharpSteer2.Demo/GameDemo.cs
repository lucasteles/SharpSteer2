// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpSteer2.Demo.PlugIns.AirCombat;
using SharpSteer2.Demo.PlugIns.Arrival;
using SharpSteer2.Demo.PlugIns.Boids;
using SharpSteer2.Demo.PlugIns.Ctf;
using SharpSteer2.Demo.PlugIns.FlowField;
using SharpSteer2.Demo.PlugIns.GatewayPathFollowing;
using SharpSteer2.Demo.PlugIns.LowSpeedTurn;
using SharpSteer2.Demo.PlugIns.MapDrive;
using SharpSteer2.Demo.PlugIns.MeshPathFollowing;
using SharpSteer2.Demo.PlugIns.MultiplePursuit;
using SharpSteer2.Demo.PlugIns.OneTurning;
using SharpSteer2.Demo.PlugIns.Pedestrian;
using SharpSteer2.Demo.PlugIns.Soccer;
using SharpSteer2.Helpers;

namespace SharpSteer2.Demo;

/// <summary>
/// This is the main type for your game
/// </summary>
public class GameDemo : Game
{
    // these are the size of the offscreen drawing surface
    // in general, no one wants to change these as there
    // are all kinds of UI calculations and positions based
    // on these dimensions.

    // these are the size of the output window, ignored
    // on Xbox 360
    const int PreferredWindowWidth = 1024;
    const int PreferredWindowHeight = 640;

    public readonly GraphicsDeviceManager Graphics;

    Effect effect;
    EffectParameter effectParamWorldViewProjection;

    SpriteFont font;
    SpriteBatch spriteBatch;

    public Matrix WorldMatrix;
    public Matrix ViewMatrix;
    public Matrix ProjectionMatrix;

    readonly List<TextEntry> texts;

    // currently selected plug-in (user can choose or cycle through them)
    static PlugIn selectedPlugIn;

    // currently selected vehicle.  Generally the one the camera follows and
    // for which additional information may be displayed.  Clicking the mouse
    // near a vehicle causes it to become the Selected Vehicle.
    public static IVehicle SelectedVehicle;

    public static readonly Clock Clock = new();
    public static readonly Camera Camera = new();

    // some camera-related default constants
    public const float Camera2DElevation = 8;
    public const float CameraTargetDistance = 13;

    readonly Annotation annotations = new();

    public GameDemo()
    {
        Graphics = new(this);
        Content.RootDirectory = "Content";
        Graphics.PreferredBackBufferWidth = PreferredWindowWidth;
        Graphics.PreferredBackBufferHeight = PreferredWindowHeight;

        Drawing.Game = this;
        Annotation.Drawer = new();

// ReSharper disable ObjectCreationAsStatement
//Constructing these silently updates a static list of all constructed plugins (euch)
        new FlowFieldPlugIn(annotations);
        new ArrivalPlugIn(annotations);
        new MeshPathFollowingPlugin(annotations);
        new GatewayPathFollowingPlugin(annotations);
        new AirCombatPlugin(annotations);
        new BoidsPlugIn(annotations);
        new LowSpeedTurnPlugIn(annotations);
        new PedestrianPlugIn(annotations);
        new CtfPlugIn(annotations);
        new MapDrivePlugIn(annotations);
        new MpPlugIn(annotations);
        new SoccerPlugIn(annotations);
        new OneTurningPlugIn(annotations);
// ReSharper restore ObjectCreationAsStatement

        texts = [];
        IsMouseVisible = true;
        IsFixedTimeStep = true;
    }

    public static void Init2dCamera(IVehicle selected, float distance = CameraTargetDistance,
        float elevation = Camera2DElevation)
    {
        Position2dCamera(selected, distance, elevation);
        Camera.FixedDistanceDistance = distance;
        Camera.FixedDistanceVerticalOffset = elevation;
        Camera.Mode = Camera.CameraMode.FixedDistanceOffset;
    }

    public static void Init3dCamera(IVehicle selected, float distance = CameraTargetDistance,
        float elevation = Camera2DElevation)
    {
        Position3dCamera(selected, distance);
        Camera.FixedDistanceDistance = distance;
        Camera.FixedDistanceVerticalOffset = elevation;
        Camera.Mode = Camera.CameraMode.FixedDistanceOffset;
    }

    public static void Position2dCamera(IVehicle selected, float distance = CameraTargetDistance,
        float elevation = Camera2DElevation)
    {
        // position the camera as if in 3d:
        Position3dCamera(selected, distance);

        // then adjust for 3d:
        var position3D = Camera.Position;
        position3D.Y += elevation;
        Camera.Position = position3D;
    }

    public static void Position3dCamera(IVehicle selected, float distance = CameraTargetDistance)
    {
        SelectedVehicle = selected;
        if (selected is not null)
        {
            var behind = selected.Forward * -distance;
            Camera.Position = selected.Position + behind;
            Camera.Target = selected.Position;
        }
    }

    // camera updating utility used by several (all?) plug-ins
    public static void UpdateCamera(float elapsedTime, IVehicle selected)
    {
        Camera.VehicleToTrack = selected;
        Camera.Update(elapsedTime, Clock.PausedState);
    }

    // ground plane grid-drawing utility used by several plug-ins
    public static void GridUtility(System.Numerics.Vector3 gridTarget)
    {
        // Math.Round off target to the nearest multiple of 2 (because the
        // checkboard grid with a pitch of 1 tiles with a period of 2)
        // then lower the grid a bit to put it under 2d annotation lines
        var gridCenter = new Vector3((float)(Math.Round(gridTarget.X * 0.5f) * 2),
            (float)(Math.Round(gridTarget.Y * 0.5f) * 2) - .05f,
            (float)(Math.Round(gridTarget.Z * 0.5f) * 2));

        // colors for checkboard
        var gray1 = new Color(new Vector3(0.27f));
        var gray2 = new Color(new Vector3(0.30f));

        // draw 50x50 checkerboard grid with 50 squares along each side
        Drawing.DrawXzCheckerboardGrid(50, 50, gridCenter.ToNumerics(), gray1, gray2);

        // alternate style
        //Bnoerj.AI.Steering.Draw.drawXZLineGrid(50, 50, gridCenter, Color.Black);
    }

    // draws a gray disk on the XZ plane under a given vehicle
    public static void HighlightVehicleUtility(IVehicle vehicle)
    {
        if (vehicle is not null)
        {
            Drawing.DrawXzDisk(vehicle.Radius, vehicle.Position, Color.LightGray, 20);
        }
    }

    // draws a gray circle on the XZ plane under a given vehicle
    public static void CircleHighlightVehicleUtility(IVehicle vehicle)
    {
        if (vehicle is not null)
        {
            Drawing.DrawXzCircle(vehicle.Radius * 1.1f, vehicle.Position, Color.LightGray, 20);
        }
    }

    // draw a box around a vehicle aligned with its local space
    // xxx not used as of 11-20-02
    public static void DrawBoxHighlightOnVehicle(IVehicle v, Color color)
    {
        if (v is not null)
        {
            var diameter = v.Radius * 2;
            var size = new Vector3(diameter, diameter, diameter);
            Drawing.DrawBoxOutline(v, size.ToNumerics(), color);
        }
    }

    // draws a colored circle (perpendicular to view axis) around the center
    // of a given vehicle.  The circle's radius is the vehicle's radius times
    // radiusMultiplier.
    public static void DrawCircleHighlightOnVehicle(IVehicle v, float radiusMultiplier, Color color)
    {
        if (v is not null)
        {
            var cPosition = Camera.Position;
            Drawing.Draw3dCircle(
                v.Radius * radiusMultiplier, // adjusted radius
                v.Position, // center
                v.Position - cPosition, // view axis
                color, // drawing color
                20); // circle segments
        }
    }

    // Find the AbstractVehicle whose screen position is nearest the current the
    // mouse position.  Returns NULL if mouse is outside this window or if
    // there are no AbstractVehicle.
    internal static IVehicle VehicleNearestToMouse() => null; //findVehicleNearestScreenPosition(mouseX, mouseY);

    // Find the AbstractVehicle whose screen position is nearest the given window
    // coordinates, typically the mouse position.  Returns NULL if there are no
    // AbstractVehicles.
    //
    // This works by constructing a line in 3d space between the camera location
    // and the "mouse point".  Then it measures the distance from that line to the
    // centers of each AbstractVehicle.  It returns the AbstractVehicle whose
    // distance is smallest.
    //
    // xxx Issues: Should the distanceFromLine test happen in "perspective space"
    // xxx or in "screen space"?  Also: I think this would be happy to select a
    // xxx vehicle BEHIND the camera location.
    internal static IVehicle FindVehicleNearestScreenPosition(int x, int y)
    {
        // find the direction from the camera position to the given pixel
        var direction = DirectionFromCameraToScreenPosition(x, y);

        // iterate over all vehicles to find the one whose center is nearest the
        // "eye-mouse" selection line
        var minDistance = float.MaxValue; // smallest distance found so far
        IVehicle nearest = null; // vehicle whose distance is smallest
        var vehicles = AllVehiclesOfSelectedPlugIn();
        foreach (var vehicle in vehicles)
        {
            // distance from this vehicle's center to the selection line:
            var d = vehicle.Position.DistanceFromLine(Camera.Position, direction.ToNumerics());

            // if this vehicle-to-line distance is the smallest so far,
            // store it and this vehicle in the selection registers.
            if (d < minDistance)
            {
                minDistance = d;
                nearest = vehicle;
            }
        }

        return nearest;
    }

    // return a normalized direction vector pointing from the camera towards a
    // given point on the screen: the ray that would be traced for that pixel
    static Vector3 DirectionFromCameraToScreenPosition(int x, int y)
    {
#if TODO
			// Get window height, viewport, modelview and projection matrices
			// Unproject mouse position at near and far clipping planes
			gluUnProject(x, h - y, 0, mMat, pMat, vp, &un0x, &un0y, &un0z);
			gluUnProject(x, h - y, 1, mMat, pMat, vp, &un1x, &un1y, &un1z);

			// "direction" is the normalized difference between these far and near
			// unprojected points.  Its parallel to the "eye-mouse" selection line.
			Vector3 diffNearFar = new Vector3(un1x - un0x, un1y - un0y, un1z - un0z);
			Vector3 direction = diffNearFar.normalize();
			return direction;
#else
        return Vector3.Up;
#endif
    }

    // select the "next" plug-in, cycling through "plug-in selection order"
    static void SelectDefaultPlugIn()
    {
        PlugIn.SortBySelectionOrder();
        selectedPlugIn = PlugIn.FindDefault();
    }

    // open the currently selected plug-in
    static void OpenSelectedPlugIn()
    {
        Camera.Reset();
        SelectedVehicle = null;
        selectedPlugIn.Open();
    }

    static void ResetSelectedPlugIn() => selectedPlugIn.Reset();

    static void CloseSelectedPlugIn()
    {
        selectedPlugIn.Close();
        SelectedVehicle = null;
    }

    // return a group (an STL vector of AbstractVehicle pointers) of all
    // vehicles(/agents/characters) defined by the currently selected PlugIn
    static IEnumerable<IVehicle> AllVehiclesOfSelectedPlugIn() => selectedPlugIn.Vehicles;

    // select the "next" vehicle: the one listed after the currently selected one
    // in allVehiclesOfSelectedPlugIn
    static void SelectNextVehicle()
    {
        if (SelectedVehicle is not null)
        {
            // get a container of all vehicles
            var all = AllVehiclesOfSelectedPlugIn().ToArray();

            // find selected vehicle in container
            var i = Array.FindIndex(all, v => v is not null && v == SelectedVehicle);
            if (i >= 0 && i < all.Length)
            {
                SelectedVehicle = i == all.Length - 1 ? all[0] : all[i + 1];
            }
            else
            {
                // if the search failed, use NULL
                SelectedVehicle = null;
            }
        }
    }

    static void UpdateSelectedPlugIn(float currentTime, float elapsedTime)
    {
        // switch to Update phase
        PushPhase(Phase.Update);

        // service queued reset request, if any
        DoDelayedResetPlugInXxx();

        // if no vehicle is selected, and some exist, select the first one
        if (SelectedVehicle == null)
        {
            var all = AllVehiclesOfSelectedPlugIn().ToArray();
            if (all.Length > 0)
                SelectedVehicle = all[0];
        }

        // invoke selected PlugIn's Update method
        selectedPlugIn.Update(currentTime, elapsedTime);

        // return to previous phase
        PopPhase();
    }

    static bool delayedResetPlugInXxx;
    internal static void QueueDelayedResetPlugInXxx() => delayedResetPlugInXxx = true;

    static void DoDelayedResetPlugInXxx()
    {
        if (!delayedResetPlugInXxx) return;
        ResetSelectedPlugIn();
        delayedResetPlugInXxx = false;
    }

    static void PushPhase(Phase newPhase)
    {
        // update timer for current (old) phase: add in time since last switch
        UpdatePhaseTimers();

        // save old phase
        phaseStack[phaseStackIndex++] = phase;

        // set new phase
        phase = newPhase;

        // check for stack overflow
        if (phaseStackIndex >= PhaseStackSize)
        {
            throw new InvalidOperationException("Phase stack has overflowed");
        }
    }

    static void PopPhase()
    {
        // update timer for current (old) phase: add in time since last switch
        UpdatePhaseTimers();

        // restore old phase
        phase = phaseStack[--phaseStackIndex];
    }

    // redraw graphics for the currently selected plug-in
    static void RedrawSelectedPlugIn(float currentTime, float elapsedTime)
    {
        // switch to Draw phase
        PushPhase(Phase.Draw);

        // invoke selected PlugIn's Draw method
        selectedPlugIn.Redraw(currentTime, elapsedTime);

        // draw any annotation queued up during selected PlugIn's Update method
        Drawing.AllDeferredLines();
        Drawing.AllDeferredCirclesOrDisks();

        // return to previous phase
        PopPhase();
    }

    int frameRatePresetIndex;

    // cycle through frame rate presets  (XXX move this to OpenSteerDemo)
    void SelectNextPresetFrameRate()
    {
        // note that the cases are listed in reverse order, and that
        // the default is case 0 which causes the index to wrap around
        switch (++frameRatePresetIndex)
        {
            case 3:
                // animation mode at 60 fps
                Clock.FixedFrameRate = 60;
                Clock.AnimationMode = true;
                Clock.VariableFrameRateMode = false;
                break;
            case 2:
                // real-time fixed frame rate mode at 60 fps
                Clock.FixedFrameRate = 60;
                Clock.AnimationMode = false;
                Clock.VariableFrameRateMode = false;
                break;
            case 1:
                // real-time fixed frame rate mode at 24 fps
                Clock.FixedFrameRate = 24;
                Clock.AnimationMode = false;
                Clock.VariableFrameRateMode = false;
                break;
            default:
                // real-time variable frame rate mode ("as fast as possible")
                frameRatePresetIndex = 0;
                Clock.FixedFrameRate = 0;
                Clock.AnimationMode = false;
                Clock.VariableFrameRateMode = true;
                break;
        }
    }

    static void SelectNextPlugin()
    {
        CloseSelectedPlugIn();
        selectedPlugIn = selectedPlugIn.Next();
        OpenSelectedPlugIn();
    }

    /// <summary>
    /// Allows the game to perform any initialization it needs to before starting to run.
    /// This is where it can query for any required services and load any non-graphic
    /// related content.  Calling base.Initialize will enumerate through any components
    /// and initialize them as well.
    /// </summary>
    protected override void Initialize()
    {
        SelectDefaultPlugIn();
        OpenSelectedPlugIn();

        base.Initialize();
    }

    /// <summary>
    /// Load your graphics content.  If loadAllContent is true, you should
    /// load content from both ResourceManagementMode pools.  Otherwise, just
    /// load ResourceManagementMode.Manual content.
    /// </summary>
    protected override void LoadContent()
    {
        base.LoadContent();
        font = Content.Load<SpriteFont>("Fonts/main");

        spriteBatch = new(Graphics.GraphicsDevice);

        effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
        };

        effectParamWorldViewProjection = effect.Parameters["WorldViewProj"];
    }

    /// <summary>
    /// Unload your graphics content.  If unloadAllContent is true, you should
    /// unload content from both ResourceManagementMode pools.  Otherwise, just
    /// unload ResourceManagementMode.Manual content.  Manual content will get
    /// Disposed by the GraphicsDevice during a Reset.
    /// </summary>
    protected override void UnloadContent() => Content.Unload();

    KeyboardState prevKeyState;

    bool IsKeyDown(KeyboardState keyState, Keys key) => prevKeyState.IsKeyDown(key) == false && keyState.IsKeyDown(key);

    protected override void Update(GameTime gameTime)
    {
        var padState = GamePad.GetState(PlayerIndex.One);
        var keyState = Keyboard.GetState();
        if (padState.Buttons.Back == ButtonState.Pressed || keyState.IsKeyDown(Keys.Escape))
            Exit();

        if (IsKeyDown(keyState, Keys.R))
            ResetSelectedPlugIn();
        if (IsKeyDown(keyState, Keys.S))
            SelectNextVehicle();
        if (IsKeyDown(keyState, Keys.A))
            annotations.IsEnabled = !annotations.IsEnabled;
        if (IsKeyDown(keyState, Keys.Space))
            Clock.TogglePausedState();
        if (IsKeyDown(keyState, Keys.C))
            Camera.SelectNextMode();
        if (IsKeyDown(keyState, Keys.F))
            SelectNextPresetFrameRate();
        if (IsKeyDown(keyState, Keys.Tab))
            SelectNextPlugin();

        for (var key = Keys.F1; key <= Keys.F10; key++)
            if (IsKeyDown(keyState, key))
                selectedPlugIn.HandleFunctionKeys(key);

        prevKeyState = keyState;

        // update global simulation clock
        Clock.Update();

        //  start the phase timer (XXX to accurately measure "overhead" time this
        //  should be in displayFunc, or somehow account for time outside this
        //  routine)
        InitPhaseTimers();

        // run selected PlugIn (with simulation's current time and step size)
        UpdateSelectedPlugIn(Clock.TotalSimulationTime, Clock.ElapsedSimulationTime);

        WorldMatrix = Matrix.Identity;

        var pos = Camera.Position;
        var lookAt = Camera.Target;
        var up = Camera.Up;
        ViewMatrix = Matrix.CreateLookAt(new(pos.X, pos.Y, pos.Z), new(lookAt.X, lookAt.Y, lookAt.Z),
            new(up.X, up.Y, up.Z));

        ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(45), // 45 degree angle
            Graphics.GraphicsDevice.Viewport.Width / (float)Graphics.GraphicsDevice.Viewport.Height,
            1.0f, 400.0f);

        base.Update(gameTime);
    }

    /// <summary>
    /// This is called when the game should draw itself.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    protected override void Draw(GameTime gameTime)
    {
        Graphics.GraphicsDevice.Clear(Color.CornflowerBlue);

        Graphics.GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        Graphics.GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        Graphics.GraphicsDevice.RasterizerState = RasterizerState.CullNone;

        var worldViewProjection = WorldMatrix * ViewMatrix * ProjectionMatrix;
        effectParamWorldViewProjection.SetValue(worldViewProjection);


        effect.CurrentTechnique.Passes[0].Apply();

        // redraw selected PlugIn (based on real time)
        RedrawSelectedPlugIn(Clock.TotalRealTime, Clock.ElapsedRealTime);

        // Draw some sample text.
        spriteBatch.Begin();

        var cw = font.MeasureString("M").X;
        float lh = font.LineSpacing;

        foreach (var text in texts)
            spriteBatch.DrawString(font, text.Text, text.Position, text.Color);
        texts.Clear();

        // get smoothed phase timer information
        var ptd = PhaseTimerDraw;
        var ptu = PhaseTimerUpdate;
        var pto = PhaseTimerOverhead;
        var smoothRate = Clock.SmoothingRate;
        Utilities.BlendIntoAccumulator(smoothRate, ptd, ref smoothedTimerDraw);
        Utilities.BlendIntoAccumulator(smoothRate, ptu, ref smoothedTimerUpdate);
        Utilities.BlendIntoAccumulator(smoothRate, pto, ref smoothedTimerOverhead);

        // keep track of font metrics and start of next line
        var screenLocation = new Vector2(cw, lh / 2);

        spriteBatch.DrawString(font, $"Camera: {Camera.ModeName}", screenLocation, Color.White);
        screenLocation.Y += lh;
        spriteBatch.DrawString(font, $"PlugIn: {selectedPlugIn.Name}", screenLocation, Color.White);

        screenLocation = new(cw, PreferredWindowHeight - 5.5f * lh);

        spriteBatch.DrawString(font, $"Update: {GetPhaseTimerFps(smoothedTimerUpdate)}",
            screenLocation, Color.White);
        screenLocation.Y += lh;
        spriteBatch.DrawString(font, $"Draw:   {GetPhaseTimerFps(smoothedTimerDraw)}", screenLocation,
            Color.White);
        screenLocation.Y += lh;
        spriteBatch.DrawString(font, $"Other:  {GetPhaseTimerFps(smoothedTimerOverhead)}",
            screenLocation, Color.White);
        screenLocation.Y += 1.5f * lh;

        // target and recent average frame rates
        var targetFps = Clock.FixedFrameRate;
        var smoothedFps = Clock.SmoothedFps;

        // describe clock mode and frame rate statistics
        var sb = new StringBuilder();
        sb.Append("Clock: ");
        if (Clock.AnimationMode)
        {
            var ratio = smoothedFps / targetFps;
            sb.AppendFormat("animation mode ({0} fps, display {1} fps {2}% of nominal speed)",
                targetFps, Math.Round(smoothedFps), (int)(100 * ratio));
        }
        else
        {
            sb.Append("real-time mode, ");
            if (Clock.VariableFrameRateMode)
            {
                sb.AppendFormat("variable frame rate ({0} fps)", Math.Round(smoothedFps));
            }
            else
            {
                sb.AppendFormat("fixed frame rate (target: {0} actual: {1}, ", targetFps, Math.Round(smoothedFps));

                // create usage description character string
                var str = $"usage: {Clock.SmoothedUsage:0}%";
                var x = screenLocation.X + sb.Length * cw;

                for (var i = 0; i < str.Length; i++)
                    sb.Append(" ");
                sb.Append(")");

                // display message in lower left corner of window
                // (draw in red if the instantaneous usage is 100% or more)
                var usage = Clock.Usage;
                spriteBatch.DrawString(font, str, new(x, screenLocation.Y), usage >= 100 ? Color.Red : Color.White);
            }
        }

        spriteBatch.DrawString(font, sb.ToString(), screenLocation, Color.White);

        spriteBatch.End();

        base.Draw(gameTime);
    }

    static string GetPhaseTimerFps(float phaseTimer)
    {
        // different notation for variable and fixed frame rate
        if (Clock.VariableFrameRateMode)
        {
            // express as FPS (inverse of phase time)
            return $"{phaseTimer:0.00000} ({1 / phaseTimer:0} FPS)";
        }

        // quantify time as a percentage of frame time
        double fps = Clock.FixedFrameRate; // 1.0f / TargetElapsedTime.TotalSeconds;
        return $"{phaseTimer:0.00000} ({100.0f * phaseTimer / (1.0f / fps):0}% of 1/{(int)fps}sec)";
    }

    enum Phase
    {
        Overhead,
        Update,
        Draw,
        Count
    }

    static Phase phase;
    const int PhaseStackSize = 5;
    static readonly Phase[] phaseStack = new Phase[PhaseStackSize];
    static int phaseStackIndex;
    static readonly float[] phaseTimers = new float[(int)Phase.Count];
    static float phaseTimerBase;

    // draw text showing (smoothed, rounded) "frames per second" rate
    // (and later a bunch of related stuff was dumped here, a reorg would be nice)
    static float smoothedTimerDraw;
    static float smoothedTimerUpdate;
    static float smoothedTimerOverhead;

    public static bool IsDrawPhase => phase == Phase.Draw;

    static float PhaseTimerDraw => phaseTimers[(int)Phase.Draw];

    static float PhaseTimerUpdate => phaseTimers[(int)Phase.Update];
    // XXX get around shortcomings in current implementation, see note
    // XXX in updateSimulationAndRedraw
#if IGNORE
		float phaseTimerOverhead
		{
			get { return phaseTimers[(int)Phase.overheadPhase]; }
		}
#else
    static float PhaseTimerOverhead => Clock.ElapsedRealTime - (PhaseTimerDraw + PhaseTimerUpdate);
#endif

    static void InitPhaseTimers()
    {
        phaseTimers[(int)Phase.Draw] = 0;
        phaseTimers[(int)Phase.Update] = 0;
        phaseTimers[(int)Phase.Overhead] = 0;
        phaseTimerBase = Clock.TotalRealTime;
    }

    static void UpdatePhaseTimers()
    {
        var currentRealTime = Clock.RealTimeSinceFirstClockUpdate();
        phaseTimers[(int)phase] += currentRealTime - phaseTimerBase;
        phaseTimerBase = currentRealTime;
    }

    public void AddText(TextEntry text) => texts.Add(text);
}
