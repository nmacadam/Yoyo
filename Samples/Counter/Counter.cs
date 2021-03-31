// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yoyo.Runtime;

namespace Yoyo.Samples
{
    public class Counter : NetworkBehaviour
    {
		[SerializeField] private int _currentValue = 0;
		[SerializeField] private Text _text = default;

		public void SetValue(int value)
		{
			_currentValue = value;
			_text.text = _currentValue.ToString();
		}

        public override void HandleMessage(string flag, string value)
        {
			if (IsClient && flag == "NEXT")
			{
				SetValue(int.Parse(value));
			}
        }

        public override IEnumerator SlowUpdate()
        {
			while (true)
			{
				if (IsServer)
				{
					SetValue(_currentValue + 1);
					SendUpdate("NEXT", _currentValue.ToString());
				}

				yield return new WaitForSeconds(1f);
			}
        }
    }
}