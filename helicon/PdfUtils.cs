
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using IronRockUtils;
using System.Linq;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using iText.Kernel.Font;
using iText.Layout;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.PdfCleanup;
using iText.PdfCleanup.Autosweep;

namespace helicon
{
	public class Color
	{
		public readonly float r;
		public readonly float g;
		public readonly float b;
		
		public Color (string hex)
		{
			this.r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255.0f;
			this.g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255.0f;
			this.b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255.0f;
		}
	}

	public class TextChar
	{
		public float x1, y1, x2, y2;
		public string text;

	    public string fontFamily;
	    public float fontSize;

	    public TextChar()
	    {
	    	this.text = " ";
	    }

	    public void UpdateRect (TextWord word)
	    {
	    	y1 = word.y1;
	    	y2 = word.y2;
	    }
	}

	public class TextWord
	{
	    public float x1, y1, x2, y2;
	    public string text;

	    public List<TextChar> chars;
	    
	    public TextWord()
	    {
	    	this.chars = new List<TextChar> ();
	    }

	    public bool AddChar (TextChar c)
	    {
			if (chars.Count == 0)
			{
				this.x1 = c.x1;
				this.x2 = c.x2;
				this.text = c.text;

				chars.Add(c);
				return true;
			}

			if (c.x1 - chars[chars.Count-1].x2 < 2)
			{
				this.text += c.text;
				this.x2 = c.x2;

				chars.Add(c);
				return true;
			}

			return false;
	    }

	    public void Finish (TextLine line)
	    {
	    	this.y1 = line.y1;
	    	this.y2 = line.y2;

	    	foreach (TextChar c in chars)
	    		c.UpdateRect(this);
	    }
	}

	public class TextLine
	{
		public float x1, y1, x2, y2;
	    public string text;

		public List<TextWord> words;
		public List<TextChar> chars;

		public TextLine()
		{
			chars = new List<TextChar> ();
		}

		public bool AddChar (TextChar c)
		{
			float cy = (c.y1 + c.y2) / 2;

			if (chars.Count == 0)
			{
				this.y1 = c.y1;
				this.y2 = c.y2;

				chars.Add(c);
				return true;
			}

			if (this.y1 <= cy && cy <= this.y2)
			{
				if (c.y1 < this.y1) this.y1 = c.y1;
				if (c.y2 > this.y2) this.y2 = c.y2;

				chars.Add(c);
				return true;
			}

			return false;
		}

		public void Finish ()
		{
			chars = chars.OrderBy((TextChar a) => a.x1).ToList();
			var nchars = new List<TextChar> ();

			words = new List<TextWord> ();
	    	TextWord word = null;

	    	foreach (TextChar c in chars)
	    	{
	    		if (word == null || word.AddChar(c) == false)
	    		{
	    			if (word != null)
	    			{
	    				TextChar c2 = new TextChar();

						c2.x1 = c.x1;
						c2.y1 = c.y1;
						c2.x2 = c.x2;
						c2.y2 = c.y2;
						c2.fontFamily = c.fontFamily;
						c2.fontSize = c.fontSize;

	    				nchars.Add(c2);
	    			}

	    			word = new TextWord();
	    			words.Add(word);
	    			word.AddChar(c);
	    		}

	    		nchars.Add(c);
	    	}

	    	chars = nchars;

	    	this.x1 = words[0].x1;
	    	this.x2 = words[words.Count-1].x2;
			this.text = null;

			foreach (TextWord w in words)
			{
				text = text == null ? w.text : (text + " " + w.text);
				w.Finish(this);
			}
			
			if (this.text.Length != this.chars.Count)
				throw new Exception("Error while creating lines from PDF.");
		}

		public TextChar GetChar(int i)
		{
			return i < 0 || i >= chars.Count ? null : chars[i];
		}

		public Rectangle GetRect (int i, int n)
		{
			TextChar start = GetChar(i);
			TextChar end = GetChar(i+n-1);

			return start == null || end == null ? null : new Rectangle (start.x1, start.y1, end.x2 - start.x1 + 1, end.y2 - start.y1 + 1);
		}
	}

	class TextLocationStrategy : LocationTextExtractionStrategy
	{
	    private List<TextChar> chars = new List<TextChar>();
	    private List<TextLine> lines;

	    public List<TextLine> GetLines()
	    {
	    	return lines;
		}
	    
	    public void Reset()
	    {
	    	chars = new List<TextChar> ();
	    	lines = null;
	    }

	    public void Finish()
	    {
	    	chars = chars.OrderByDescending ((TextChar a) => a.y1).ToList();

	    	lines = new List<TextLine> ();
	    	TextLine line = null;

	    	foreach (TextChar c in chars)
	    	{
	    		if (line == null || line.AddChar(c) == false)
	    		{
	    			line = new TextLine();
	    			lines.Add(line);
	    			line.AddChar(c);
	    		}
	    	}

	    	foreach (TextLine l in lines)
	    		l.Finish();
		}

	    public override void EventOccurred(IEventData data, EventType type)
	    {
	        if (!type.Equals(EventType.RENDER_TEXT))
	            return;

	        TextRenderInfo renderInfo = (TextRenderInfo)data;

	        string curFont = renderInfo.GetFont().GetFontProgram().ToString();

	        float curFontSize = renderInfo.GetFontSize();

	        IList<TextRenderInfo> text = renderInfo.GetCharacterRenderInfos();
	        foreach (TextRenderInfo t in text)
	        {
	            string letter = t.GetText();

	            Vector letterStart = t.GetDescentLine().GetStartPoint();
	            Vector letterEnd = t.GetAscentLine().GetEndPoint();

	            if (letter != " " && !letter.Contains(" "))
	            {
	                TextChar c = new TextChar();
	                chars.Add(c);

	                c.text = letter;
	                c.x1 = letterStart.Get(0);
					c.x2 = letterEnd.Get(0);

	                c.y1 = letterStart.Get(1);
	                c.y2 = letterEnd.Get(1);

	                if (c.y1 > c.y2)
	                {
		                c.y1 = letterEnd.Get(1);
		                c.y2 = letterStart.Get(1);
	                }

	                c.fontFamily = curFont;
	                c.fontSize = curFontSize;
	            }
	        }
	    }
	}

	public class PdfUtils
	{
		public static string GetText (string filename)
		{
			if (!System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + "poppler\\pdftohtml.exe"))
				throw new Exception ("Dependency tool 'pdftohtml' was not found.");

			string html = Utils.Run("CMD.EXE", "/C " + AppDomain.CurrentDomain.BaseDirectory + "poppler\\pdftohtml.exe -q -i -noframes -wbt 30 -stdout \"" + filename + "\"");
			return HtmlUtils.ConvertHtml(html);
		}

		public static void SlicePDF (string outputFile, string inputFile, int[] pageStart, int[] pageEnd)
		{
			PdfDocument src = new PdfDocument(new PdfReader(inputFile));

			PdfWriter writer = new PdfWriter (new FileStream(outputFile, FileMode.Create));
			PdfDocument pdfDocument = new PdfDocument (writer);
			pdfDocument.SetTagged();

			PdfMerger merger = new PdfMerger (pdfDocument);

			int n = Math.Min(pageStart.Length, pageEnd.Length);
			for (int i = 0; i < n; i++)
			{
				if (pageStart[i] < 0 || pageEnd[i] < 0)
					continue;

				int lastPage = pageEnd[i];

				if (lastPage > src.GetNumberOfPages())
					lastPage = src.GetNumberOfPages();

				merger.Merge(src, pageStart[i], lastPage);
			}

			src.Close();
			pdfDocument.Close();
		}

		public static void MergePDFs (string outputPath, string[] inputPath)
		{
			bool newMode = true;

			if (newMode)
			{
				List<PdfDocument> documentList = new List<PdfDocument>();

				foreach (string inputFile in inputPath)
				{
					documentList.Add (new PdfDocument(new PdfReader(inputFile)));
				}

				PdfWriter writer = new PdfWriter (new FileStream(outputPath, FileMode.Create));
				PdfDocument pdfDocument = new PdfDocument (writer);
				pdfDocument.SetTagged();

				PdfMerger merger = new PdfMerger (pdfDocument);

				foreach (PdfDocument doc in documentList)
				{
					merger.Merge(doc, 1, doc.GetNumberOfPages());
					//doc.CopyPagesTo(1, doc.GetNumberOfPages(), pdfDocument);
					doc.Close();
				}

				pdfDocument.Close();
			}
			else
			{
				List<PdfDocument> documentList = new List<PdfDocument>();
	
				foreach (string inputFile in inputPath)
				{
					documentList.Add (new PdfDocument(new PdfReader(inputFile)));
				}

				PdfWriter writer = new PdfWriter (new FileStream(outputPath, FileMode.Create));
				PdfDocument pdfDocument = new PdfDocument (writer);
				pdfDocument.SetTagged();
	
				PdfMerger merger = new PdfMerger (pdfDocument);
	
				foreach (PdfDocument doc in documentList)
				{
					merger.Merge(doc, 1, doc.GetNumberOfPages());
					doc.Close();
				}
	
				pdfDocument.Close();
			}
		}

		public static void StampPDF (string src, string dest, int pageNum, int margin, int padding, string offsX, string offsY, float dx, float dy, float fontSize, string text)
        {
			PdfDocument pdfDoc = new PdfDocument(new PdfReader(src), new PdfWriter(dest));

			Rectangle pageSize;
			PdfCanvas canvas;

			float width = 0;
			float height = 0;

			PdfFont font = PdfFontFactory.CreateFont (iText.IO.Font.Constants.StandardFonts.HELVETICA, iText.IO.Font.PdfEncodings.UTF8, true);

			string[] lines = text.Split('\n');

			height = lines.Length;
			for (int l = 0; l < lines.Length; l++)
			{
				lines[l] = lines[l].Trim();

				int w = (int)font.GetWidth(lines[l], fontSize);
				if (w > width) width = w;
			}

			float fontHeight = fontSize*1.2f;
			float fontWidth = fontSize*0.5f;

			height *= fontHeight;
			
			width += 2*padding;
			height += 2*padding;

			int i = pageNum;
			{
				PdfPage page = pdfDoc.GetPage(i);
				pageSize = page.GetPageSize();

				float x = 0;
				float y = 0;

				switch (offsX.ToUpper())
				{
					case "LEFT":
						x = pageSize.GetLeft() + margin;
						break;

					case "RIGHT":
						x = pageSize.GetRight() - margin - width;
						break;

					case "CENTER":
						x = (pageSize.GetLeft() + pageSize.GetRight() - width) / 2;
						break;
				}

				switch (offsY.ToUpper())
				{
					case "TOP":
						y = pageSize.GetTop() - margin;
						break;

					case "BOTTOM":
						y = pageSize.GetBottom() + margin + height;
						break;

					case "CENTER":
						y = (pageSize.GetTop() + pageSize.GetBottom() + height) / 2;
						break;
				}

				x += dx;
				y += dy;

				canvas = new PdfCanvas(page);

				canvas.SetFillColorRgb(1, 1, 1);
				canvas.SetStrokeColorRgb(1, 0, 0);

				canvas.Rectangle(x, y - height, width, height);
				canvas.Fill();

				canvas.Rectangle(x, y - height, width, height);
				canvas.Stroke();

				canvas.SetFillColorRgb(1, 0, 0);

				for (int l = 0; l < lines.Length; l++)
				{
					canvas.BeginText();
					canvas.SetFontAndSize(font, fontSize);
					canvas.MoveText(x+padding, y - fontHeight*0.7 - fontHeight*l - padding);
					canvas.ShowText(lines[l]);
					canvas.EndText();
				}
			}

			pdfDoc.Close();
		}

		public static PdfDocument OpenPDF (string src, string dest=null)
		{
			if (dest != null)
				return new PdfDocument(new PdfReader(src), new PdfWriter(dest));

			return new PdfDocument(new PdfReader(src));
		}
		
		public static void ClosePDF (PdfDocument doc)
		{
			doc.Close();
		}

		public static List<Dictionary<string, object>> FindMatches (PdfDocument pdf, string regex, int minPage=0, int numPages=0, int count=0)
		{
			FilteredEventListener listener = new FilteredEventListener();
			TextLocationStrategy s = listener.AttachEventListener(new TextLocationStrategy());
			PdfCanvasProcessor processor = new PdfCanvasProcessor(listener);

			int maxNumPages = pdf.GetNumberOfPages();

			if (minPage <= 0 || minPage > maxNumPages) minPage = 1;
			if (numPages <= 0 || numPages > maxNumPages) numPages = maxNumPages;

			List<Dictionary<string, object>> list = new List<Dictionary<string, object>> ();

			for (int pageNum = minPage; maxNumPages != 0; maxNumPages--, pageNum++)
			{
				s.Reset();
				PdfPage page = pdf.GetPage(pageNum);
				processor.ProcessPageContent(page);
				s.Finish();

				foreach (TextLine x in s.GetLines())
				{
					while (true)
					{
						Match m = Regex.Match(x.text, regex, RegexOptions.IgnoreCase);
						if (!m.Success) break;
	
						int i = x.text.IndexOf(m.Value);
						int n = m.Value.Length;

						x.text = x.text.Substring(0, i) + new String('X', n) + x.text.Substring(i+n);

						Rectangle r = x.GetRect(i, n);
						if (r == null) continue;
	
						Dictionary<string, object> data = new Dictionary<string, object> ();
						list.Add(data);
	
						data["Page"] = pageNum;
						data["Text"] = m.Value;
						data["X"] = r.GetLeft();
						data["Y"] = r.GetBottom();
						data["Width"] = r.GetWidth();
						data["Height"] = r.GetHeight();
						data["FontSize"] = x.chars[0].fontSize;
						data["FontFamily"] = x.chars[0].fontFamily;
						
						if (count != 0 && data.Count >= count)
							return list;
					}
				}
			}

			return list;
		}

		public static void Remove (PdfDocument pdf, int pageNum, float x, float y, float width, float height)
		{
			PdfPage page = pdf.GetPage(pageNum);

			List<PdfCleanUpLocation> cleanUpLocations = new List<PdfCleanUpLocation> ();
			cleanUpLocations.Add(new PdfCleanUpLocation(pageNum, new Rectangle(x, y, width, height)));
			PdfCleanUpTool cleaner = new PdfCleanUpTool(pdf, cleanUpLocations);
			cleaner.CleanUp();
		}

		public static void Overlay (PdfDocument pdf, int pageNum, float x, float y, float width, float height, float fontSize, string background, string foreground, string text)
		{
			PdfFont font = PdfFontFactory.CreateFont (iText.IO.Font.Constants.StandardFonts.HELVETICA, iText.IO.Font.PdfEncodings.UTF8, true);
			PdfPage page = pdf.GetPage(pageNum);

			float fWidth = font.GetWidth(text, fontSize);
			float fHeight = fontSize*1.1f;

			Rectangle r = new Rectangle (x, y, Math.Max(fWidth, width), Math.Max(fHeight, height));
			Color bg = new Color (background);
			Color fg = new Color (foreground);

			PdfCanvas pdfCanvas = new PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), page.GetDocument());
			pdfCanvas.SetFillColorRgb(bg.r, bg.g, bg.b);
			pdfCanvas.Rectangle(r);
			pdfCanvas.Fill();

			if (text != "")
			{
				pdfCanvas.SetFillColorRgb(fg.r, fg.g, fg.b);
				Canvas canvas = new Canvas(pdfCanvas, pdf, new Rectangle(r.GetLeft(), r.GetBottom(), r.GetWidth(), r.GetHeight()));
	
				iText.Layout.Element.Paragraph p = new iText.Layout.Element.Paragraph(text);

				p.SetMarginLeft( (r.GetWidth() - fWidth) * 0.5f );
				p.SetMarginTop( (r.GetHeight() - fHeight) * 0.5f );
				p.SetFont(font);
				p.SetFontSize(fontSize);

				canvas.Add(p);
			}
		}
	}
}
