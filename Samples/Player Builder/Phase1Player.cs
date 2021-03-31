// Nathan MacAdam

using System.Collections;
using Yoyo.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Lab4.Phase1
{
	public enum Phase1Shapes
	{
		Cube,
		Sphere,
		Cylinder
	}

	[System.Serializable]
	public struct PlayerOptions
	{
		[SerializeField] public int NetId;
		[SerializeField] public string Name;
		[SerializeField] public Phase1Shapes Shape;
		[SerializeField] public Color Color;
	}

	[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
	public class Phase1Player : NetworkBehaviour
	{
		[Header("Player Info")]
		[SerializeField] private PlayerOptions _options = default;

		[Header("Shapes")]
		[SerializeField] private Mesh _cube = default;
		[SerializeField] private Mesh _sphere = default;
		[SerializeField] private Mesh _cylinder = default;

		private MeshFilter _meshFilter;
		private MeshRenderer _meshRenderer;
		[SerializeField] private Text _text; 

		private bool _isInitialized = false;

		private Vector3 _initialPosition;

        public Mesh GetMesh(Phase1Shapes shape)
		{
			switch (shape)
			{
				case Phase1Shapes.Cube: return _cube;
				case Phase1Shapes.Sphere: return _sphere;
				case Phase1Shapes.Cylinder: return _cylinder;
			}

			return _cube;
		}

		private void Start() 
		{
			_meshRenderer = GetComponent<MeshRenderer>();
			_meshFilter = GetComponent<MeshFilter>();
			_text = GetComponentInChildren<Text>();
			
			_initialPosition = transform.position;
		}

		public void ApplyProperties(PlayerOptions options)
		{
			ApplyProperties(options.Name, options.Shape, options.Color);
		}

		public void ApplyProperties(string name, Phase1Shapes shape, Color color)
		{
			_isInitialized = true;

			_meshRenderer = GetComponent<MeshRenderer>();
			_meshFilter = GetComponent<MeshFilter>();
			_text = GetComponentInChildren<Text>();

			_options.Name = name;
			_options.Shape = shape;
			_options.Color = color;

			name = _options.Name;
			_text.text = _options.Name;

			_meshRenderer.material.color = _options.Color;
			_meshFilter.mesh = GetMesh(_options.Shape);
		}

        public override void HandleMessage(string flag, string value)
        {
			if (IsServer)
			{
				if (flag == "REQUESTUPDATE")
				{
					Debug.Log("Sending update");
					SendUpdate("UPDATEPLAYER", JsonUtility.ToJson(_options));
				}
			}

			if (IsClient && !IsLocalPlayer)
			{
				if (flag == "UPDATEPLAYER")
				{
					var options = JsonUtility.FromJson<PlayerOptions>(value);
					ApplyProperties(options);
					Debug.Log("Received update");
				}
			}
        }

        public override IEnumerator SlowUpdate()
        {
			int connections = Session.Connections.Count;

			while (true)
			{
				_options.NetId = NetId;
				transform.position = _initialPosition + Vector3.right * _options.NetId;

				if (_isInitialized)
				{
					SendCommand("REQUESTUPDATE", "1");
				}

				yield return null;
			}
        }
	}
}