// Nathan MacAdam

using Yoyo.Runtime;
using UnityEngine;

namespace Tanks
{
	public class Startup : MonoBehaviour
	{
		[SerializeField] private YoyoSession _networkCore = default;
		//[SerializeField] private GameObject _serverUI = default;
		[SerializeField] private ClientLogin _clientLogin = default;
		
		public void OnStartServer()
		{
			//_networkCore.Environment = YoyoEnvironment.Server;

			_networkCore.StartServer();

			gameObject.SetActive(false);
			//_serverUI.SetActive(true);
		}

		public void OnStartClient()
		{
			//_networkCore.Environment = YoyoEnvironment.Client;

			_clientLogin.Login();
			gameObject.SetActive(false);
		}
	}
}