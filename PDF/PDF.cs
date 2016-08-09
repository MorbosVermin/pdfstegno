using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PDF
{

    public sealed class PDFHeader
    {

        public string Version
        {
            get;
            set;
        }

        public bool isBinary
        {
            get;
            set;
        }

        public PDFHeader(string version, bool isBinary)
        {
            this.Version = version;
            this.isBinary = isBinary;
        }

        public PDFHeader(string version) : this(version, false) {}

        public PDFHeader()  : this("1.1", false) {}

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.Append(String.Format("%PDF-{0}", Version) + PDFDoc.CRLF);
            if(isBinary)
                str.Append("%\xD0\xD0\xD0\xD0" + PDFDoc.CRLF);

            return str.ToString();
        }

    }

    public sealed class PDFXRef
    {

        public int Offset
        {
            get;
            set;
        }

        public int GenerationNumber
        {
            get;
            set;
        }

        public string Flags
        {
            get;
            set;
        }

        public PDFXRef(int offset, int genNumber, string flags)
        {
            Offset = offset;
            GenerationNumber = genNumber;
            Flags = flags;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} {2}", Offset.ToString("D10"), GenerationNumber.ToString("D5"), Flags);
        }

    }

    public sealed class PDFXRefs : CollectionBase
    {

        public PDFXRefs()
            : base()
        {
            this.Add(new PDFXRef(0, 65535, "f"));
        }

        public int Add(PDFXRef xref)
        {
            return List.Add(xref);
        }

        public bool Contains(PDFXRef xref)
        {
            foreach(PDFXRef xr in this)  
            {
                if (xref.Offset == xr.Offset)
                    return true;
            }

            return false;
        }

        public PDFXRef this[int index]
        {
            get { return (PDFXRef)List[index]; }
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.Append("xref" + PDFDoc.CRLF);
            str.Append("0 " + (Count - 1) + PDFDoc.CRLF);
            foreach (PDFXRef xref in this)
            {
                str.Append(xref.ToString() + PDFDoc.CRLF);
            }

            return str.ToString();
        }

    }

    public sealed class PDFTrailor
    {
        public int Start
        {
            get;
            set;
        }

        public int Size
        {
            get;
            set;
        }

        public string Root
        {
            get;
            set;
        }

        public string Info
        {
            get;
            set;
        }

        public string Previous
        {
            get;
            set;
        }

        public PDFTrailor(int start, int size, string root, string info)
        {
            Start = start;
            Size = size;
            Root = root;
            Info = info;
            Previous = null;
        }

        public PDFTrailor(int start, int size, string root) : this(start, size, root, null) { }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.Append("trailor" + PDFDoc.CRLF);
            str.Append(String.Format("<<" + PDFDoc.CRLF + " /Size {0}" + PDFDoc.CRLF, Size));
            str.Append(String.Format(" /Root {0}" + PDFDoc.CRLF, Root));
            
            if (Previous != null)
                str.Append(String.Format(" /Previous {0}"+ PDFDoc.CRLF, Previous));

            if (Info != null)
                str.Append(String.Format(" /Info {0}" + PDFDoc.CRLF, Info));
            
            str.Append(String.Format(">>" + PDFDoc.CRLF + "startxref" + PDFDoc.CRLF + "{0}" + PDFDoc.CRLF + "%%EOF", Start));
            return str.ToString();
        }

    }

    public enum PDFObjectTypes
    {
        Catalog,
        Outlines,
        Pages,
        Page
    }

    public sealed class PDFObject
    {

        public int Index
        {
            get;
            set;
        }

        public int Version
        {
            get;
            set;
        }

        public PDFObjectTypes Type
        {
            get;
            set;
        }

        public string Data
        {
            get;
            set;
        }
        public PDFObject(int index, int version, PDFObjectTypes type, string data)
        {
            Index = index;
            Version = version;
            Type = type;
            Data = data;
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            str.Append(String.Format("%d %d obj", Index, Version) + PDFDoc.CRLF);
            str.Append(String.Format("<<" + PDFDoc.CRLF +" /Type /%s", Type) + PDFDoc.CRLF);
            str.Append(Data + PDFDoc.CRLF);
            str.Append(">>" + PDFDoc.CRLF +"endobj");

            return str.ToString();
        }

    }

    public sealed class PDFDoc
    {
        public static string CRLF { get { return "\n"; } }

        public string Path
        {
            get;
            set;
        }

        public int Size
        {
            get
            {
                return File.ReadAllBytes(Path).Length;
            }
        }

        public PDFHeader Header
        {
            get;
            set;
        }

        private PDFXRefs References;

        public PDFDoc(string path, bool binary)
        {
            References = new PDFXRefs();
            Path = path;
            if (!File.Exists(Path))
            {
                Header = new PDFHeader();
                Header.isBinary = binary;
            }

        }

        public PDFDoc(string path) : this(path, false) { }

        public void StartNew()
        {
            using (StreamWriter writer = new StreamWriter(File.Open(Path, FileMode.OpenOrCreate)))
            {
                writer.Write(Header.ToString());
                writer.Flush();
            }
        }

        public void Append(string line)
        {
            if (!line.EndsWith(CRLF))
                line += CRLF;

            using (StreamWriter writer = new StreamWriter(File.Open(Path, FileMode.Append)))
            {
                writer.Write(line);
                writer.Flush();
            }
        }

        public void AppendNewLine()
        {
            Append("");
        }

        public void Append(byte[] data)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(Path, FileMode.Append), Encoding.ASCII))
            {
                writer.Write(data, 0, data.Length);
                writer.Flush();
            }
        }

        public void AppendComment(string comment)
        {
            Append(String.Format("%% %s", comment));
        }

        public static string ToHex(byte[] data)
        {
            StringBuilder b = new StringBuilder(data.Length * 2);
            foreach(byte d in data)  
            {
                b.AppendFormat("{0:x2}", d);
            }

            return b.ToString();
        }

        public void AppendHexEncodedStream(int index, int version, byte[] data)
        {
            string eData = ToHex(data);
            AppendNewLine();
            References.Add(new PDFXRef(index, Size, "n"));
            Append(String.Format("{0} {1} obj" + CRLF + "<<"+ CRLF +" /Length {2}"+ CRLF +" /Filter ASCIIHexDecode"+ CRLF +">>", index, version, eData.Length));
            Append(Encoding.ASCII.GetBytes(eData));
            Append(CRLF + "endstream" + CRLF + "endobj");
        }

        public void AppendXRef()
        {
            AppendNewLine();
            Append(References.ToString());
        }

        public void AppendTrailor(string root)
        {
            PDFTrailor trailer = new PDFTrailor(Size, References.Count - 1, root);
            AppendNewLine();
            Append(trailer.ToString());
        }

        public void IndirectObject(int index, int version, string io)
        {
            AppendNewLine();
            References.Add(new PDFXRef(Size, version, "n"));
            Append(String.Format("{0} {1} obj"+ CRLF +"{2}"+ CRLF +"endobj", index, version, io));
        }

    }

}
