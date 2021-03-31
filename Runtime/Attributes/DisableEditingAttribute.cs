// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System;
using UnityEngine;

namespace Yoyo.Attributes
{
	/// <summary>
	/// Disables editing the field in the inspector but still displays it
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class DisableEditingAttribute : PropertyAttribute
	{ }
}