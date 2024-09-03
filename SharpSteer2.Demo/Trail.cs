using System;
using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo;

/// <summary>
/// Provides support to visualize the recent path of a vehicle.
/// </summary>
public class Trail
{
    int currentIndex; // Array index of most recently recorded point
    readonly float sampleInterval; // Desired interval between taking samples
    float lastSampleTime; // Global time when lat sample was taken
    int dottedPhase; // Dotted line: draw segment or not
    Vector3 currentPosition; // Last reported position of vehicle
    readonly Vector3[] vertices; // Array (ring) of recent points along trail
    readonly byte[] flags; // Array (ring) of flag bits for trail points

    /// <summary>
    /// Initializes a new instance of Trail.
    /// </summary>
    public Trail()
        : this(5, 100) { }

    /// <summary>
    /// Initializes a new instance of Trail.
    /// </summary>
    /// <param name="duration">The amount of time the trail represents.</param>
    /// <param name="vertexCount">The number of smaples along the trails length.</param>
    public Trail(float duration, int vertexCount)
    {
        // Set internal trail state
        currentIndex = 0;
        lastSampleTime = 0;
        sampleInterval = duration / vertexCount;
        dottedPhase = 1;

        // Initialize ring buffers
        vertices = new Vector3[vertexCount];
        flags = new byte[vertexCount];

        TrailColor = Color.LightGray;
        TickColor = Color.White;
    }

    /// <summary>
    /// Gets or sets the color of the trail.
    /// </summary>
    public Color TrailColor { get; set; }

    /// <summary>
    /// Gets or sets the color of the ticks.
    /// </summary>
    public Color TickColor { get; set; }

    /// <summary>
    /// Records a position for the current time, called once per update.
    /// </summary>
    /// <param name="currentTime"></param>
    /// <param name="position"></param>
    public void Record(float currentTime, Vector3 position)
    {
        var timeSinceLastTrailSample = currentTime - lastSampleTime;
        if (timeSinceLastTrailSample > sampleInterval)
        {
            currentIndex = (currentIndex + 1) % vertices.Length;
            vertices[currentIndex] = position;
            dottedPhase = (dottedPhase + 1) % 2;
            var tick = Math.Floor(currentTime) > Math.Floor(lastSampleTime);
            flags[currentIndex] = (byte)(dottedPhase | (tick ? 2 : 0));
            lastSampleTime = currentTime;
        }

        currentPosition = position;
    }

    /// <summary>
    /// Draws the trail as a dotted line, fading away with age.
    /// </summary>
    public void Draw(IAnnotationService annotation)
    {
        var index = currentIndex;
        for (var j = 0; j < vertices.Length; j++)
        {
            // index of the next vertex (mod around ring buffer)
            var next = (index + 1) % vertices.Length;

            // "tick mark": every second, draw a segment in a different color
            var tick = (flags[index] & 2) != 0 || (flags[next] & 2) != 0;
            var color = tick ? TickColor : TrailColor;

            // draw every other segment
            if ((flags[index] & 1) != 0)
            {
                if (j == 0)
                {
                    // draw segment from current position to first trail point
                    annotation.Line(currentPosition, vertices[index], color.ToVector3().ToNumerics());
                }
                else
                {
                    // draw trail segments with opacity decreasing with age
                    const float minO = 0.05f; // minimum opacity
                    var fraction = (float)j / vertices.Length;
                    var opacity = (fraction * (1 - minO)) + minO;
                    annotation.Line(vertices[index], vertices[next], color.ToVector3().ToNumerics(), opacity);
                }
            }

            index = next;
        }
    }

    /// <summary>
    /// Clear trail history. Used to prevent long streaks due to teleportation.
    /// </summary>
    public void Clear()
    {
        currentIndex = 0;
        lastSampleTime = 0;
        dottedPhase = 1;

        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = Vector3.Zero;
            flags[i] = 0;
        }
    }
}
