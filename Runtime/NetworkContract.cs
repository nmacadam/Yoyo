// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections.Generic;
using UnityEngine;

namespace Yoyo.Runtime
{
	[CreateAssetMenu(fileName = "Network Contract", menuName = "Yoyo/Network Contract")]
	public class NetworkContract : ScriptableObject
	{
		[SerializeField] private List<GameObject> _prefabs = new List<GameObject>();

		public const int DefaultIndex = 0;

		public GameObject GetDefaultObject()
		{
			if (_prefabs.Count == 0)
			{
				Debug.LogError("No default object to return", this);
				return null;
			}

			return _prefabs[DefaultIndex];
		}

		public GameObject GetPrefab(int index)
		{
			if (index < 0 || index >= _prefabs.Count)
			{
				Debug.LogError($"yoyo - prefab index '{index}' out of range", this);
				return null;
			}

			return _prefabs[index];
		}

		public int GetIndex(GameObject prefab)
		{
			return _prefabs.IndexOf(prefab);
		}

		public bool IsValidIndex(int index)
		{
			return !(index < 0 || index >= _prefabs.Count);
		}

		public bool ContainsPrefab(GameObject prefab)
		{
			return _prefabs.Contains(prefab);
		}

		#if UNITY_EDITOR
		private void OnValidate() 
		{
			//  ! dont like this :(())
			if (_prefabs == null) return;
			if (_prefabs.Count == 0) return;

			for (int i = 0; i < _prefabs.Count; i++)
			{
				if (_prefabs[i] == null) 
				{
					continue;
				}

				NetworkEntity entity = _prefabs[i].GetComponent<NetworkEntity>();
				if (entity != null)
				{
					entity.SetPrefabId(i);
				}
			}
		}
		#endif
	}
}