// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using UnityEngine;

namespace Yoyo.Attributes
{
	/// <summary>
	/// Displays a dropdown with options for assigning a numeric value
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class NumberDropdownAttribute : PropertyAttribute
	{
        private int[] _integerValues;
        private float[] _floatValues;

		public int[] IntegerValues => _integerValues;
		public float[] FloatValues => _floatValues;

        public bool IsInt { get; private set; }

		public NumberDropdownAttribute(params int[] values)
		{
            _integerValues = values;
            IsInt = true;
		}

        public NumberDropdownAttribute(params float[] values)
        {
            _floatValues = values;
            IsInt = false;
        }
    }
}