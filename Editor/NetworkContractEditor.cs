// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Yoyo.Runtime;

namespace Yoyo.Editors
{
	[CustomEditor(typeof(NetworkContract))]
	public class NetworkContractEditor : Editor
	{
		private Color _firstColor = new Color(255f / 255f, 170f / 255f, 110f / 255f, 1f);
		private GUIContent _errorIcon;

		private SerializedProperty _prefabs;  
		private ReorderableList _list; 

		private void OnEnable()
		{
			_prefabs = serializedObject.FindProperty("_prefabs");

			_errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml");
			_errorIcon.tooltip = "Missing NetworkEntity Component";

			_list = new ReorderableList(serializedObject, _prefabs, true, true, true, true);
			_list.drawElementCallback = DrawListItems;
        	_list.drawHeaderCallback = DrawHeader;
		}

		private void DrawListItems(Rect rect, int index, bool isActive, bool isFocused)
		{
			SerializedProperty element = _list.serializedProperty.GetArrayElementAtIndex(index);
			GUIContent label = new GUIContent("Type ID: " + index.ToString());

			rect.y += 1.0f;
			rect.x += 10.0f;
			rect.width -= 10.0f;

			NetworkContract contract = (NetworkContract)element.serializedObject.targetObject;
			GameObject prefab = contract.GetPrefab(index);
			if (prefab != null)
			{
				NetworkEntity entity = prefab.GetComponent<NetworkEntity>();
				if (entity == null)
				{
					Rect iconPosition = new Rect(
						rect.x,//EditorGUIUtility.labelWidth, 
						rect.y, 
						_errorIcon.image.height + 2, 
						_errorIcon.image.height + 2
					);
					EditorGUI.LabelField(iconPosition, _errorIcon);

					rect.x += _errorIcon.image.width + 2;
					rect.width -= _errorIcon.image.width + 2;
				}
			}

			if (index == 0)
			{
				label.text += " (Default Object)";
				var color = GUI.backgroundColor;
				GUI.backgroundColor = _firstColor;
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
					element,
					label,
					true
				); 
				GUI.backgroundColor = color;
			}
			else
			{
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
					element,
					label,
					true
				); 
			}
		}

		private void DrawHeader(Rect rect)
		{
    		EditorGUI.LabelField(rect, "Prefabs");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			_list.DoLayoutList();

			serializedObject.ApplyModifiedProperties();
		}
	}
}