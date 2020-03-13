
using System;
using System.Collections.Generic;
using System.IO;
using IronRockUtils;

using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using iText.Layout;

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
	}
}
