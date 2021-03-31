// Nathan MacAdam

using System.Collections;
using Yoyo.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Lab4.Phase1
{
	public class Phase1Manager : NetworkBehaviour
	{
		[SerializeField] private GameObject _optionsUI = default;
		[SerializeField] private int _playerPrefabId = 1;

		[Header("Options UI")]
		[SerializeField] private GameObject _optionsContent = default;
		[SerializeField] private Dropdown _colorDropdown = default;
		[SerializeField] private Dropdown _shapeDropdown = default;
		[SerializeField] private InputField _nameField = default;

		private Phase1Player _player;

		private bool _isDirty = false;
		private PlayerOptions _options;

		public void Submit() 
		{
			PlayerOptions options = new PlayerOptions();
			options.Color = GetColor(_colorDropdown.value);
			options.Shape = GetShape(_shapeDropdown.value);
			options.Name = _nameField.text;

			string json = JsonUtility.ToJson(options);
			SendCommand("PLAYEROPTIONS", json);

			Debug.Log("sending options: " + json);
		}

		private Color GetColor(int color)
		{
			switch (color)
			{
				case 0: return Color.red;
				case 1: return Color.green;
				case 2: return Color.blue;
			}
			return Color.red;
		}

		private Phase1Shapes GetShape(int shape)
		{
			switch (shape)
			{
				case 0: return Phase1Shapes.Cube;
				case 1: return Phase1Shapes.Sphere;
				case 2: return Phase1Shapes.Cylinder;
			}
			return Phase1Shapes.Cube;
		}

        public override void HandleMessage(string flag, string value)
        {
			if (IsServer)
			{
				if (flag == "PLAYEROPTIONS")
				{
					PlayerOptions options = JsonUtility.FromJson<PlayerOptions>(value);

					GameObject go = Session.NetCreateObject(_playerPrefabId, Owner);
					_player = go.GetComponent<Phase1Player>();
					_player.ApplyProperties(options);
					Debug.Log("received options: " + value);

					SendUpdate("PLAYEROPTIONS", value);
					Debug.Log("sent options: " + value);
				}
			}

			if (IsClient && IsLocalPlayer)
			{
				if (flag == "PLAYEROPTIONS")
				{
					_isDirty = true;
					_options = JsonUtility.FromJson<PlayerOptions>(value);

					var players = FindObjectsOfType<Phase1Player>();
					foreach (var player in players)
					{
						if (player.NetId == _options.NetId)
						{
							Debug.Log("Applied update to " + _options.NetId);
							player.ApplyProperties(_options);
						}
					}

					Debug.Log("received options: " + value);
				}
			}
        }

        public override IEnumerator SlowUpdate()
        {
			while (true)
			{
				if (!IsLocalPlayer)
				{
					_optionsContent.SetActive(false);
				}

				yield return null;
			}
        }
	}
}