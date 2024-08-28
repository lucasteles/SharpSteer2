using System;
using Microsoft.Xna.Framework;
using SharpSteer2;
using Vector3 = System.Numerics.Vector3;

namespace Demo
{
	/// <summary>
	/// Provides support to visualize the recent path of a vehicle.
	/// </summary>
	public class Trail
	{
		int _currentIndex;			// Array index of most recently recorded point
	    readonly float _sampleInterval;		// Desired interval between taking samples
		float _lastSampleTime;		// Global time when lat sample was taken
		int _dottedPhase;			// Dotted line: draw segment or not
		Vector3 _currentPosition;	// Last reported position of vehicle
	    readonly Vector3[] _vertices;			// Array (ring) of recent points along trail
	    readonly byte[] _flags;				// Array (ring) of flag bits for trail points
		Color _trailColor;			// Color of the trail
		Color _tickColor;			// Color of the ticks

		/// <summary>
		/// Initializes a new instance of Trail.
		/// </summary>
		public Trail()
			: this(5, 100)
		{
		}

		/// <summary>
		/// Initializes a new instance of Trail.
		/// </summary>
		/// <param name="duration">The amount of time the trail represents.</param>
		/// <param name="vertexCount">The number of smaples along the trails length.</param>
		public Trail(float duration, int vertexCount)
		{
		    // Set internal trail state
			_currentIndex = 0;
			_lastSampleTime = 0;
			_sampleInterval = duration / vertexCount;
			_dottedPhase = 1;

			// Initialize ring buffers
			_vertices = new Vector3[vertexCount];
			_flags = new byte[vertexCount];

			_trailColor = Color.LightGray;
			_tickColor = Color.White;
		}

		/// <summary>
		/// Gets or sets the color of the trail.
		/// </summary>
		public Color TrailColor
		{
			get { return _trailColor; }
			set { _trailColor = value; }
		}

		/// <summary>
		/// Gets or sets the color of the ticks.
		/// </summary>
		public Color TickColor
		{
			get { return _tickColor; }
			set { _tickColor = value; }
		}

		/// <summary>
		/// Records a position for the current time, called once per update.
		/// </summary>
		/// <param name="currentTime"></param>
		/// <param name="position"></param>
		public void Record(float currentTime, Vector3 position)
		{
			float timeSinceLastTrailSample = currentTime - _lastSampleTime;
			if (timeSinceLastTrailSample > _sampleInterval)
			{
				_currentIndex = (_currentIndex + 1) % _vertices.Length;
				_vertices[_currentIndex] = position;
				_dottedPhase = (_dottedPhase + 1) % 2;
				bool tick = (Math.Floor(currentTime) > Math.Floor(_lastSampleTime));
				_flags[_currentIndex] = (byte)(_dottedPhase | (tick ? 2 : 0));
				_lastSampleTime = currentTime;
			}
			_currentPosition = position;
		}

		/// <summary>
		/// Draws the trail as a dotted line, fading away with age.
		/// </summary>
		public void Draw(IAnnotationService annotation)
		{
			int index = _currentIndex;
			for (int j = 0; j < _vertices.Length; j++)
			{
				// index of the next vertex (mod around ring buffer)
				int next = (index + 1) % _vertices.Length;

				// "tick mark": every second, draw a segment in a different color
				bool tick = ((_flags[index] & 2) != 0 || (_flags[next] & 2) != 0);
				Color color = tick ? _tickColor : _trailColor;

				// draw every other segment
				if ((_flags[index] & 1) != 0)
				{
					if (j == 0)
					{
						// draw segment from current position to first trail point
                        annotation.Line(_currentPosition, _vertices[index], color.ToVector3().FromXna());
					}
					else
					{
						// draw trail segments with opacity decreasing with age
						const float MIN_O = 0.05f; // minimum opacity
						float fraction = (float)j / _vertices.Length;
						float opacity = (fraction * (1 - MIN_O)) + MIN_O;
                        annotation.Line(_vertices[index], _vertices[next], color.ToVector3().FromXna(), opacity);
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
			_currentIndex = 0;
			_lastSampleTime = 0;
			_dottedPhase = 1;

			for (int i = 0; i < _vertices.Length; i++)
			{
				_vertices[i] = Vector3.Zero;
				_flags[i] = 0;
			}
		}
	}
}
