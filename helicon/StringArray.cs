
using System;

namespace helicon
{
	public class StringArray
	{
		public string[] values;
		public int Length;

		public StringArray(string[] values)
		{
			this.values = values;
			this.Length = this.values.Length;
		}

		public StringArray(string value, char delimiter)
		{
			this.values = value != null ? value.Split(delimiter) : null;
			this.Length = value != null ? this.values.Length : 0;
		}

		public StringArray Trim()
		{
			for (int i = 0; i < Length; i++)
				values[i] = values[i].Trim();

			return this;
		}

		public StringArray Clip()
		{
			int n = 0;

			for (int i = 0; i < Length; i++)
				if (!String.IsNullOrEmpty(values[i])) n++;

			if (n == Length) return this;

			string[] temp = new string[n];

			n = 0;

			for (int i = 0; i < Length; i++)
				if (!String.IsNullOrEmpty(values[i])) temp[n++] = values[i];

			this.values = temp;
			this.Length = temp.Length;

			return this;
		}

		public StringArray ToUpper()
		{
			for (int i = 0; i < Length; i++)
				values[i] = values[i].ToUpper();

			return this;
		}

		public int IndexOf(string value)
		{
			for (int i = 0; i < Length; i++)
			{
				if (values[i] == value)
					return i;
			}

			return -1;
		}
	}
}
