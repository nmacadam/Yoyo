// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEditor;
using UnityEngine;
using Yoyo.Attributes;

namespace Yoyo.Editor.Attributes
{
    /// <summary>
    /// Drawer for drawing NumberDropdown attribute
    /// </summary>
    [CustomPropertyDrawer(typeof(NumberDropdownAttribute))]
	public class NumberDropdownDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
            NumberDropdownAttribute numberDropdown = (NumberDropdownAttribute)attribute;

            if (numberDropdown.IsInt)
            {
                GUIContent[] displayedOptions = new GUIContent[numberDropdown.IntegerValues.Length];
                int currentIndex = -1;
                for (int i = 0; i < numberDropdown.IntegerValues.Length; i++)
                {
                    if (property.intValue == numberDropdown.IntegerValues[i])
                    {
                        currentIndex = i;
                    }

                    displayedOptions[i] = new GUIContent(numberDropdown.IntegerValues[i].ToString());
                }

                if (currentIndex == -1)
                {
                    property.intValue = numberDropdown.IntegerValues[0];
                    currentIndex = 0;
                }

                int index = EditorGUI.Popup(position, label, currentIndex, displayedOptions);
                property.intValue = numberDropdown.IntegerValues[index];
            }
            else
            {

            }
		}
	}
}