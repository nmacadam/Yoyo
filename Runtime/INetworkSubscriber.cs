// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

namespace Yoyo.Runtime
{
	public interface INetworkSubscriber
	{
		void OnEntityInitialized(NetworkEntity entity);
	}
}