
using System;
using System.Xml;

namespace helicon
{
	public class XmlUtils
	{
		public static string GetInner (XmlNode doc, string path, string def)
		{
			XmlNode node = doc.SelectSingleNode (path);
			if (node == null) return def;

			string res = node.InnerText.Trim();

			if (res == "[TAB]")
				return "\t";

			if (res == "[SPACE]")
				return " ";

			return res.Length == 0 ? def : res;
		}

		public static string[] GetInners (XmlNode doc, string path, string def)
		{
			XmlNodeList nodes = doc.SelectNodes (path);
			if (nodes == null) return def == null ? new string[] { } : new string[] { def };

			int count = 0;

			foreach (XmlNode node in nodes)
			{
				if (node.InnerText.Trim().Length != 0)
					count++;
			}

			if (count == 0) return def == null ? new string[] { } : new string[] { def };

			string[] data = new string[count];
			int i = 0;

			foreach (XmlNode node in nodes)
			{
				if (node.InnerText.Trim().Length == 0)
					continue;

				data[i++] = node.InnerText.Trim();
			}

			return data;
		}

		public static XmlElement FirstChildElement (XmlNode node)
		{
			foreach (XmlNode child in node.ChildNodes)
			{
				if (child.NodeType == XmlNodeType.Element)
					return (XmlElement)child;
			}

			return null;
		}

		public static string GetStringAttribute (XmlNode node, string name, string def)
		{
			XmlAttribute attr = node.Attributes[name];
			if (attr == null) return def;

			return attr.Value;
		}
	}
}
