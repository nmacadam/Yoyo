// Nathan MacAdam, Unity Bingo!

using System.Collections;
using Yoyo.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Tanks
{
	public class ClientLogin : MonoBehaviour
	{
		[SerializeField] private YoyoSession _networkCore = default;
		[SerializeField] private UnityEvent _onConnected = default;

		public void Login()
		{
			_networkCore.StartClient();
			StartCoroutine(ConnectionSetup());
		}

		private IEnumerator ConnectionSetup()
		{
			yield return new WaitUntil(() => _networkCore.IsConnected);
			Debug.Log("Connected");
			_onConnected.Invoke();
		}
	}
}