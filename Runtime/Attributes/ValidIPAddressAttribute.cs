// Yoyo Network Engine, 2021
// Author: Nathan MacAdam

using UnityEngine;

namespace Yoyo.Attributes
{
	/// <summary>
	/// Displays a warning if field does not contain a valid IP address
	/// </summary>
	public class ValidIPAddressAttribute : PropertyAttribute
	{
		private bool _allowEmpty;

		public bool AllowEmpty => _allowEmpty;

		public ValidIPAddressAttribute(bool allowEmpty = true)
		{
			_allowEmpty = allowEmpty;
		}
	}
}