// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEditor;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Editor.Attributes
{
	/// <summary>
	/// Drawer for drawing ValidIPAddress attribute
	/// </summary>
	[CustomPropertyDrawer(typeof(ValidIPAddressAttribute), true)]
	public class ValidIPAddressDrawer : PropertyDrawer
	{
		private const int _heightPadding = 0;
		private const int _sizePadding = 2;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			ValidIPAddressAttribute validIp = (ValidIPAddressAttribute)attribute;
			var lockOn = EditorGUIUtility.IconContent("console.erroricon.sml");
			//var lockOff = EditorGUIUtility.IconContent("LockIcon");

			lockOn.tooltip = "This field is does not contain a valid IP address";
			//lockOff.tooltip = "This field will be locked during Play Mode";

			GUIStyle lockStyle = new GUIStyle();
			lockStyle.padding = new RectOffset(0, 0, 0, 0);

			Rect iconPosition = new Rect(EditorGUIUtility.labelWidth + 2, position.y + _heightPadding, lockOn.image.height + _sizePadding, lockOn.image.height + _sizePadding);

			bool isValid = false;
			if (validIp.AllowEmpty && property.stringValue == string.Empty)
			{
				isValid = true;
			}
			else if (System.Net.IPAddress.TryParse(property.stringValue, out System.Net.IPAddress address))
			{
				isValid = true;
			}

			EditorGUI.PropertyField(position, property);

			// Unity handles lists differently than normal fields; using the LabelField approach doesn't work for lists.
			if (!isValid)
			{
				EditorGUI.LabelField(iconPosition, lockOn);
			}
		}
	}
}