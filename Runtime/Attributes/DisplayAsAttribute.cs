// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using UnityEngine;

namespace Yoyo.Attributes
{
	/// <summary>
	/// Displays a field in the inspector with the given name
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class DisplayAsAttribute : PropertyAttribute
	{ 
		private string _name;

		public string Name => _name;

		public DisplayAsAttribute(string name)
		{
			_name = name;
		}
	}
}