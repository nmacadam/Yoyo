// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEngine;

namespace Yoyo.Runtime.Utilities
{
    /// <summary>
    /// Defines a range of integer values from a minimum value to a maximum value
    /// </summary>
    [System.Serializable]
    public class RangeInt
	{
		[SerializeField] private int _min;
		[SerializeField] private int _max;

        public int Min { get => _min; set => _min = value; }
        public int Max { get => _max; set => _max = value; }

        public RangeInt(int min, int max)
        {
            _min = min;
            _max = max;
        }
    }
}