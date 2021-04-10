// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

namespace Yoyo.Runtime
{
	public static class YoyoVersionInfo
    {
        public const string Name = "Yoyo Networking Framework";
        public const string Version = "v0.1a";

        public static readonly uint ProtocolId = GetHash(Name + Version);

        private static uint GetHash(string inputString)
        {
            uint hash = 0;
            foreach (byte b in System.Text.Encoding.Unicode.GetBytes(inputString))
            {
                hash += b;
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }

            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);

            return (uint)(hash % 100000000);
        }
    }
}