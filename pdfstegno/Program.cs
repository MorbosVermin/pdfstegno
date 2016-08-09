using PDF;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace pdfstegno
{
    public class Program
    {

        /// <summary>
        /// TODO - No worky! :(
        /// 
        /// I initially used code from Didier Stevens to create a PDF file, however this does not
        /// seem to be working now with modern PDF readers. 
        /// </summary>
        /// <param name="path"></param>
        static void CreatePdf(string path)
        {
            PDFDoc doc = new PDFDoc(path);
            doc.StartNew();
            doc.IndirectObject(1, 0, "<<" + PDFDoc.CRLF + " /Type /Catalog" + PDFDoc.CRLF + " /Outlines 2 0 R" + PDFDoc.CRLF + " /Pages 3 0 R " + PDFDoc.CRLF + ">>");
            doc.IndirectObject(2, 0, "<<" + PDFDoc.CRLF + " /Type /Outlines" + PDFDoc.CRLF + " /Count 0" + PDFDoc.CRLF + ">>");
            doc.IndirectObject(3, 0, "<<" + PDFDoc.CRLF + " /Type /Pages" + PDFDoc.CRLF + " /Kids [4 0 R]" + PDFDoc.CRLF + ">>");
            doc.IndirectObject(4, 0, "<<" + PDFDoc.CRLF + " /Type /Page" + PDFDoc.CRLF + " /Parent 3 0 R" + PDFDoc.CRLF + ">>");

            doc.AppendXRef();
            doc.AppendTrailor("1 0 R");
        }

        /// <summary>
        /// Given a source (PDF) file, a destination, a name for referencing the payload within the PDF, and
        /// finally a executable path (payload), we open the source PDF, recalucate the xref with the 
        /// payload and inject the payload into the PDF which is written to destination. 
        /// </summary>
        /// <param name="source">Source PDF file.</param>
        /// <param name="destination">Destination PDF file.</param>
        /// <param name="name">Reference Name</param>
        /// <param name="execPath">Payload</param>
        static void Stegno(string source, string destination, string name, string execPath)
        {
            byte[] buffer = File.ReadAllBytes(execPath); //TODO may not work on large files.
            string payload = System.Convert.ToBase64String(buffer, 0, buffer.Length, Base64FormattingOptions.InsertLineBreaks);
            string[] base64 = payload.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            Console.WriteLine();
            Console.WriteLine("Source: " + source);
            Console.WriteLine("Payload: " + execPath + " (" + payload.Length + "b; " + base64.Length + " loc)");
            Console.WriteLine("------------------------------------------------");

            /*
             * Find xref
             * Inject content
             * Update startxref to include payload (new xref position).
             * Profit?
             */
            int xref = 0;
            int total = 0;
            Console.Write("Finding current xref location; please wait...");
            using (Stream s = File.Open(source, FileMode.Open))
            {
                while (xref == 0)
                {
                    byte[] b2 = new byte[1024];
                    int i = s.Read(b2, 0, b2.Length);
                    if (i == -1)
                        break;

                    total += i;
                    string content = Encoding.ASCII.GetString(b2, 0, b2.Length);
                    if (content.IndexOf("xref") >= 0)
                    {
                        xref = total - (i - content.IndexOf("xref"));
                    }
                }
            }
            Console.WriteLine("done: " + xref);

            if (File.Exists(destination))
                File.Delete(destination);

            int payloadTotal = 0;
            total = File.ReadAllBytes(source).Length;
            byte[] b = new byte[xref];
            using (Stream s = File.Open(source, FileMode.Open))
            {
                using (Stream d = File.Open(destination, FileMode.OpenOrCreate))
                {
                    int i = -1;
                    int t = 0;
                    while (true)
                    {
                        i = s.Read(b, 0, b.Length);
                        t += i;
                        d.Write(b, 0, i);
                        d.Flush();

                        if (t == xref)
                        {
                            Console.Write("Injecting payload into " + destination + " by name '" + name + "'...");
                            byte[] l = Encoding.ASCII.GetBytes("% " + name + PDFDoc.CRLF);
                            payloadTotal = l.Length;
                            d.Write(l, 0, l.Length);
                            d.Flush();

                            foreach (string p in base64)
                            {
                                if (p.Length == 0)
                                    continue;

                                byte[] l2 = Encoding.ASCII.GetBytes("% " + p + PDFDoc.CRLF);
                                payloadTotal += l2.Length;
                                d.Write(l2, 0, l2.Length);
                                d.Flush();
                            }
                            Console.WriteLine("done: " + payloadTotal + "bytes.");
                        }


                        if (t == total)
                            break;

                    }
                }
            }

            buffer = new byte[128];
            total = File.ReadAllBytes(destination).Length;
            using (Stream stream = File.Open(destination, FileMode.Open))
            {
                stream.Position = total - buffer.Length;
                int i = stream.Read(buffer, 0, buffer.Length);
                string l = Encoding.ASCII.GetString(buffer, 0, i);
                if (l.LastIndexOf("startxref") > 0)
                {
                    stream.Position = (total - (buffer.Length - l.LastIndexOf("startxref"))) + 10;
                    Console.Write("Updating startxref from " + xref + " to " + (xref + payloadTotal) + " at " + stream.Position + "; please wait...");
                    byte[] d = Encoding.ASCII.GetBytes(PDFDoc.CRLF + (xref + payloadTotal) + PDFDoc.CRLF + "%%EOF" + PDFDoc.CRLF);
                    stream.Write(d, 0, d.Length);
                    stream.Flush();
                    Console.WriteLine("done.");
                }
            }

        }

        /// <summary>
        /// Look within the given PDF file (path) for the given reference name and use the xref to extract 
        /// the payload. The payload is returned as a byte[]. 
        /// </summary>
        /// <param name="path">Path to the PDF file to extract the payload from.</param>
        /// <param name="name">Reference name of the payload within the PDF file.</param>
        /// <returns>byte[] the payload</returns>
        static byte[] GetPayload(string path, string name)
        {
            StringBuilder base64 = new StringBuilder();
            using (StreamReader reader = File.OpenText(path))
            {
                string line = "";
                bool start = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Equals("% " + name))
                    {
                        start = true;
                        continue;
                    }
                    else if (line.Trim().Equals("xref"))
                        break;

                    if (start)
                    {
                        base64.AppendLine(line.Substring(2));
                    }


                }

                if (!start)
                {
                    Console.WriteLine("Error: No content found by marker/name '" + name + "'");
                }
                else
                {
                    Console.WriteLine("Payload '" + name + "' retrieved: " + base64.Length + "bytes (encoded).");
                }
            }

            return (base64.Length > 0) ? System.Convert.FromBase64String(base64.ToString()) : new byte[0];
        }

        /// <summary>
        /// Thank you mister helper, helping-ton!
        /// </summary>
        static void Help()
        {
            Console.WriteLine("PDF Steganography Tool");
            Console.WriteLine();
            Console.WriteLine("Syntax: {0} <path to .pdf file> [-o <destination>] [-x <payload>] [-n <ref name>] [-e]",
                Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));
            Console.WriteLine("/o   The destination file to write results too.");
            Console.WriteLine("/n   Use this name for the marker of the content within the PDF.");
            Console.WriteLine("/x   Inject the content of this .exe into the PDF.");
            //Console.WriteLine("/N   Create a new, blank PDF file to work with rather than supply one.");
            Console.WriteLine("/e   Extract the .exe from the PDF and save to the file destination given (-o). Use -n to change name/marker to search for.");
            Console.WriteLine();
            Console.WriteLine("Example Usage: ");
            Console.WriteLine("   pdfstegno.exe /x payload.exe /o phish_hook.pdf MyInitial.pdf");
            Console.WriteLine("   pdfstegno.exe /e /o payload2.exe phish_hook.pdf");
            Environment.Exit(1);
        }

        /// <summary>
        /// Application Entry Point
        /// </summary>
        /// <param name="args">Arguments to the application.</param>
        /// <see cref="Help"/>
        static void Main(string[] args)
        {
            string name = "0x00000003";
            string source = "";
            string destination = "";
            string execPath = "";
            bool extract = false;
            bool newPdf = false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("/h") || args[i].Contains("-help"))
                    Help();

                if (args[i].Equals("/n"))
                {
                    name = args[(i + 1)];
                    i++;
                }
                else if (args[i].Equals("/o"))
                {
                    destination = args[(i + 1)];
                    i++;
                }
                else if (args[i].Equals("/x"))
                {
                    execPath = args[(i + 1)];
                    i++;
                }
                else if(args[i].Equals("/N"))
                {
                    source = Path.GetRandomFileName();
                    source = String.Format("{0}.pdf", source.Substring(0, source.LastIndexOf('.')));
                    newPdf = true;
                }
                else if (args[i].Equals("/e"))
                    extract = true;

                else
                    source = args[i];

            }

            if (source.Length == 0)
                Help();

            else if(!File.Exists(source) && newPdf)
                CreatePdf(source);

            else if(!File.Exists(source))
            {
                Console.Error.WriteLine("Error: File not found: {0}", source);
                Environment.Exit(1);
            }

            if (extract)
            {
                byte[] payload = GetPayload(source, name);
                if (payload.Length > 0)
                {
                    using (Stream s = File.Open(destination, FileMode.OpenOrCreate))
                    {
                        s.Write(payload, 0, payload.Length);
                        s.Flush();
                    }
                    Console.WriteLine("Wrote " + destination + " (" + payload.Length + "bytes)");
                }

            }
            else if (execPath.Length > 0)
            {
                Stegno(source, destination, name, execPath);
            }
            else if (destination.Length == 0)
            {
                GetPayload(source, name);
            }

            Environment.Exit(0);
        }
    }
}
