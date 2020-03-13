
using System;
using System.Text;
using HtmlAgilityPack;

namespace helicon
{
	public class HtmlUtils
	{
		public static string Convert(string path)
		{
		    HtmlDocument doc = new HtmlDocument();
		    doc.Load(path);
		
		    StringBuilder sw = new StringBuilder();
		    ConvertTo(doc.DocumentNode, sw);
		    return sw.ToString();
		}
		
		public static string ConvertHtml(string html)
		{
			HtmlDocument doc = new HtmlDocument();
		    doc.LoadHtml(html);

		    StringBuilder sw = new StringBuilder();
		    ConvertTo(doc.DocumentNode, sw);
		    return sw.ToString();
		}
		
		public static void ConvertTo(HtmlNode node, StringBuilder outText)
		{
		    string html;
		    switch (node.NodeType)
		    {
		        case HtmlNodeType.Comment:
		            break;
		
		        case HtmlNodeType.Document:
		            ConvertContentTo(node, outText);
		            break;
		
		        case HtmlNodeType.Text:
		            html = ((HtmlTextNode)node).Text;

		            if (HtmlNode.IsOverlappedClosingElement(html))
		                break;

		            if (html.Trim().Length > 0)
		            {
		            	html = html.Replace("&nbsp;", " ");
		            	html = html.Replace("&#xA0;", " ");
		            	html = html.Replace("&#160;", " ");

		            	while (html.IndexOf("  ") != -1)
		            		html = html.Replace("  ", " ");

		                outText.Append(HtmlEntity.DeEntitize(html));
		            }

		            break;

		        case HtmlNodeType.Element:
		            switch (node.Name)
		            {
		                case "p":
		                    outText.Append("\n");
		                    break;

						case "br": case "hr":
		                    outText.Append("\n");
		                	return;

		            	case "script": case "style": case "head":
			                return;
		            }

		            if (node.HasChildNodes)
		                ConvertContentTo(node, outText);

		            break;
		    }
		}
		
		private static void ConvertContentTo(HtmlNode node, StringBuilder outText)
		{
		    foreach (HtmlNode subnode in node.ChildNodes)
		    {
		        ConvertTo(subnode, outText);
		    }
		}

		public static string GetText (string filename)
		{
			return Convert(filename);
		}
	}
}
