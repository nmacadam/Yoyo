// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEngine;
using UnityEditor;
using RangeInt = Yoyo.Runtime.Utilities.RangeInt;

namespace Yoyo.Editor
{
	[CustomPropertyDrawer(typeof(RangeInt))]
	public class RangeDrawer: PropertyDrawer 
	{
		public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label) 
		{
			label = EditorGUI.BeginProperty(pos, label, prop);

			var labels = new[] { new GUIContent("Min"), new GUIContent("Max") };
			var properties = new[] { prop.FindPropertyRelative("_min"), prop.FindPropertyRelative("_max") };
			DrawMultiFieldProperty(pos, label, labels, properties);
	
			EditorGUI.EndProperty();
		}

		/// <summary>
		/// Draws a property with multiple property fields for sub-properties
		/// </summary>
		/// <param name="label">The main property label</param>
		/// <param name="subLabels">The sub-property labels</param>
		/// <param name="properties">The serialized sub-properties</param>
		private static void DrawMultiFieldProperty(Rect pos, GUIContent label, GUIContent[] subLabels, SerializedProperty[] properties)
		{
			float subLabelSpacing = 4f;
			var contentRect = EditorGUI.PrefixLabel(pos, GUIUtility.GetControlID(FocusType.Passive), label);
			DrawMultiplePropertyFields(contentRect, subLabels, properties, subLabelSpacing);
		}

		/// <summary>
		/// Draws multiple property fields within one rect
		/// </summary>
		/// <param name="pos">Rect to draw in</param>
		/// <param name="subLabels">Labels for content</param>
		/// <param name="props">Content serialized properties</param>
		/// <param name="subLabelSpacing">Spacing between content</param>
		private static void DrawMultiplePropertyFields(Rect pos, GUIContent[] subLabels, SerializedProperty[] props, float subLabelSpacing = 4f) 
		{
			// backup gui settings
			var indent     = EditorGUI.indentLevel;
			var labelWidth = EditorGUIUtility.labelWidth;
		
			// draw properties
			var propsCount = props.Length;
			var width      = (pos.width - (propsCount - 1) * subLabelSpacing) / propsCount;
			var contentPos = new Rect(pos.x, pos.y, width, pos.height);
			EditorGUI.indentLevel = 0;
			for (var i = 0; i < propsCount; i++) 
			{
				EditorGUIUtility.labelWidth = EditorStyles.label.CalcSize(subLabels[i]).x;
				EditorGUI.PropertyField(contentPos, props[i], subLabels[i]);
				contentPos.x += width + subLabelSpacing;
			}
	
			// restore gui settings
			EditorGUIUtility.labelWidth = labelWidth;
			EditorGUI.indentLevel       = indent;
		}
	}
}