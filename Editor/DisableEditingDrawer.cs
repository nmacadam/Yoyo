// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEditor;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Editor.Attributes
{
	/// <summary>
	/// Drawer for drawing DisableEditing attribute
	/// </summary>
	[CustomPropertyDrawer(typeof(DisableEditingAttribute))]
	public class DisableEditingDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			GUI.enabled = false;

			EditorGUI.PropertyField(position, property, label);

			GUI.enabled = true;
		}
	}
}