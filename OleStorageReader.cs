using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace antiGGGravity.Commands.Transfer.Core
{
    /// <summary>
    /// Reads PartAtom XML data directly from .rfa files using Windows OLE Structured Storage API.
    /// This bypasses the Revit API entirely, so it works on ANY version family without
    /// triggering the "Upgrading..." dialog.
    /// </summary>
    public static class OleStorageReader
    {
        // --- COM Interfaces and P/Invoke for OLE Structured Storage ---

        [ComImport, Guid("0000000d-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumSTATSTG
        {
            [PreserveSig] int Next(uint celt, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] System.Runtime.InteropServices.ComTypes.STATSTG[] rgelt, out uint pceltFetched);
            void Skip(uint celt);
            void Reset();
            void Clone(out IEnumSTATSTG ppenum);
        }

        [ComImport, Guid("0000000b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IStorage
        {
            void CreateStream(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out System.Runtime.InteropServices.ComTypes.IStream ppstm);
            void OpenStream(string pwcsName, IntPtr reserved1, uint grfMode, uint reserved2, out System.Runtime.InteropServices.ComTypes.IStream ppstm);
            void CreateStorage(string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStorage ppstg);
            void OpenStorage(string pwcsName, IStorage pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstg);
            void CopyTo(uint ciidExclude, IntPtr rgiidExclude, IntPtr snbExclude, IStorage pstgDest);
            void MoveElementTo(string pwcsName, IStorage pstgDest, string pwcsNewName, uint grfFlags);
            void Commit(uint grfCommitFlags);
            void Revert();
            void EnumElements(uint reserved1, IntPtr reserved2, uint reserved3, out IEnumSTATSTG ppenum);
            void DestroyElement(string pwcsName);
            void RenameElement(string pwcsOldName, string pwcsNewName);
            void SetElementTimes(string pwcsName, System.Runtime.InteropServices.ComTypes.FILETIME pctime, System.Runtime.InteropServices.ComTypes.FILETIME patime, System.Runtime.InteropServices.ComTypes.FILETIME pmtime);
            void SetClass(ref Guid clsid);
            void SetStateBits(uint grfStateBits, uint grfMask);
            void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, uint grfStatFlag);
        }

        [DllImport("ole32.dll")]
        private static extern int StgOpenStorage(
            [MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            IStorage pstgPriority,
            uint grfMode,
            IntPtr snbExclude,
            uint reserved,
            out IStorage ppstgOpen);

        private const uint STGM_READ = 0x00000000;
        private const uint STGM_SHARE_DENY_WRITE = 0x00000020;
        private const uint STGM_SHARE_EXCLUSIVE = 0x00000010;
        private const int STGTY_STREAM = 2;
        private const int STGTY_STORAGE = 1;

        /// <summary>
        /// Reads the Revit family category from the PartAtom data inside an .rfa file.
        /// Returns null if the data cannot be read.
        /// </summary>
        public static string ReadCategoryFromRfa(string rfaPath)
        {
            if (string.IsNullOrEmpty(rfaPath) || !File.Exists(rfaPath))
                return null;

            IStorage rootStorage = null;
            try
            {
                int hr = StgOpenStorage(rfaPath, null, STGM_READ | STGM_SHARE_DENY_WRITE, IntPtr.Zero, 0, out rootStorage);
                if (hr != 0 || rootStorage == null)
                {
                    // Try with SHARE_EXCLUSIVE if SHARE_DENY_WRITE fails
                    hr = StgOpenStorage(rfaPath, null, STGM_READ | STGM_SHARE_EXCLUSIVE, IntPtr.Zero, 0, out rootStorage);
                    if (hr != 0 || rootStorage == null)
                        return null;
                }

                // Try to read PartAtom from the root storage first
                string xml = TryReadStreamAsString(rootStorage, "PartAtomData");
                if (xml != null)
                    return ParseCategoryFromPartAtomXml(xml);

                // Enumerate all storages and streams looking for PartAtom data
                IEnumSTATSTG enumerator;
                rootStorage.EnumElements(0, IntPtr.Zero, 0, out enumerator);
                if (enumerator == null) return null;

                var stats = new System.Runtime.InteropServices.ComTypes.STATSTG[1];
                uint fetched;
                while (enumerator.Next(1, stats, out fetched) == 0 && fetched > 0)
                {
                    if (stats[0].type == STGTY_STORAGE)
                    {
                        // Look inside sub-storages
                        try
                        {
                            IStorage subStorage;
                            rootStorage.OpenStorage(stats[0].pwcsName, null, STGM_READ | STGM_SHARE_EXCLUSIVE, IntPtr.Zero, 0, out subStorage);
                            if (subStorage != null)
                            {
                                xml = TryReadStreamAsString(subStorage, "PartAtomData");
                                Marshal.ReleaseComObject(subStorage);
                                if (xml != null)
                                    return ParseCategoryFromPartAtomXml(xml);
                            }
                        }
                        catch { }
                    }
                    else if (stats[0].type == STGTY_STREAM && stats[0].pwcsName != null &&
                             stats[0].pwcsName.IndexOf("PartAtom", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        xml = TryReadStreamAsString(rootStorage, stats[0].pwcsName);
                        if (xml != null)
                            return ParseCategoryFromPartAtomXml(xml);
                    }
                }

                Marshal.ReleaseComObject(enumerator);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (rootStorage != null)
                    Marshal.ReleaseComObject(rootStorage);
            }

            return null;
        }

        private static string TryReadStreamAsString(IStorage storage, string streamName)
        {
            System.Runtime.InteropServices.ComTypes.IStream stream = null;
            try
            {
                storage.OpenStream(streamName, IntPtr.Zero, STGM_READ | STGM_SHARE_EXCLUSIVE, 0, out stream);
                if (stream == null) return null;

                // Get stream size
                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                stream.Stat(out stat, 1); // 1 = STATFLAG_NONAME
                long size = stat.cbSize;
                if (size <= 0 || size > 10 * 1024 * 1024) return null; // Skip if > 10MB

                byte[] buffer = new byte[size];
                IntPtr bytesReadPtr = Marshal.AllocCoTaskMem(sizeof(int));
                try
                {
                    stream.Read(buffer, (int)size, bytesReadPtr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(bytesReadPtr);
                }

                // Try UTF-8 first, then UTF-16
                string content = Encoding.UTF8.GetString(buffer);
                if (content.Contains("<") && content.Contains("category"))
                    return content;

                content = Encoding.Unicode.GetString(buffer);
                if (content.Contains("<") && content.Contains("category"))
                    return content;

                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (stream != null)
                    Marshal.ReleaseComObject(stream);
            }
        }

        private static string ParseCategoryFromPartAtomXml(string xml)
        {
            try
            {
                // Clean any BOM or leading non-XML characters
                int xmlStart = xml.IndexOf("<?xml");
                if (xmlStart < 0) xmlStart = xml.IndexOf("<entry");
                if (xmlStart < 0) xmlStart = xml.IndexOf("<feed");
                if (xmlStart < 0) return null;
                if (xmlStart > 0) xml = xml.Substring(xmlStart);

                // Trim trailing nulls
                xml = xml.TrimEnd('\0');

                XDocument xdoc = XDocument.Parse(xml);
                XNamespace atom = "http://www.w3.org/2005/Atom";

                var categoryNode = xdoc.Descendants(atom + "category").FirstOrDefault();
                if (categoryNode != null)
                {
                    string term = categoryNode.Attribute("term")?.Value;
                    return term; // Return raw OST_ term, FamilyManagerEngine will clean it
                }

                // Also try without namespace
                categoryNode = xdoc.Descendants("category").FirstOrDefault();
                if (categoryNode != null)
                {
                    string term = categoryNode.Attribute("term")?.Value;
                    return term;
                }
            }
            catch { }

            return null;
        }
    }
}
