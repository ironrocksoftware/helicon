
using System;
using System.Collections.Generic;
using System.IO;
using IronRockUtils;

using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using iText.Kernel.Font;
using iText.Layout;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas;

namespace helicon
{
	public class PdfUtils
	{
		public static string GetText (string filename)
		{
			if (!System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + "poppler\\pdftohtml.exe"))
				throw new Exception ("Dependency tool 'pdftohtml' was not found.");

			string html = Utils.Run("CMD.EXE", "/C " + AppDomain.CurrentDomain.BaseDirectory + "poppler\\pdftohtml.exe -q -i -noframes -wbt 30 -stdout \"" + filename + "\"");
			return HtmlUtils.ConvertHtml(html);
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
					doc.CopyPagesTo(1, doc.GetNumberOfPages(), pdfDocument);
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
	
		public static void StampPDF (string src, string dest, int pageNum, int margin, int padding, string offsX, string offsY, float fontSize, string text)
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

				int w = font.GetWidth(lines[l]) / 70;
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
	}
}
