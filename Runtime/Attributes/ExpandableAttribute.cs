// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

// Expandable attribute code from
// https://forum.unity.com/threads/editor-tool-better-scriptableobject-inspector-editing.484393/

using System;
using UnityEngine;

namespace Yoyo.Attributes
{
	/// <summary>
	/// Allows editing a ScriptableObject directly from a reference field
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class ExpandableAttribute : PropertyAttribute
	{}
}