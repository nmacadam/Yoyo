// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEditor;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Editor.Attributes
{
	/// <summary>
	/// Drawer for drawing DisplayAs attribute
	/// </summary>
	[CustomPropertyDrawer(typeof(DisplayAsAttribute))]
	public class DisplayAsDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			DisplayAsAttribute display = (DisplayAsAttribute)attribute;

			label.text = display.Name;
			EditorGUI.PropertyField(position, property, label);
		}
	}
}