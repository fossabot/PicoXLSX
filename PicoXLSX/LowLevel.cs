﻿/*
 * PicoXLSX is a small .NET library to generate XLSX (Microsoft Excel 2007 or newer) files in an easy and native way
 * Copyright Raphael Stoeckli © 2019
 * This library is licensed under the MIT License.
 * You find a copy of the license in project folder or on: http://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PicoXLSX
{
    /// <summary>
    /// Class for low level handling (XML, formatting, packing)
    /// </summary>
    /// <remarks>This class is only for internal use. Use the high level API (e.g. class Workbook) to manipulate data and create Excel files</remarks>
    class LowLevel
    {

        #region staticFields
        private static DocumentPath WORKBOOK = new DocumentPath("workbook.xml", "xl/");
        private static DocumentPath STYLES = new DocumentPath("styles.xml", "xl/");
        private static DocumentPath APP_PROPERTIES = new DocumentPath("app.xml", "docProps/");
        private static DocumentPath CORE_PROPERTIES = new DocumentPath("core.xml", "docProps/");
        private static DocumentPath SHARED_STRINGS = new DocumentPath("sharedStrings.xml", "xl/");
        #endregion

        #region privateFields
        private CultureInfo culture;
        private Workbook workbook;
        private SortedMap sharedStrings;
        private int sharedStringsTotalCount;
        private Dictionary<string, XmlDocument> interceptedDocuments;
        private bool interceptDocuments;
        #endregion

        #region properties
        /// <summary>
        /// Gets or set whether XML documents are intercepted during creation
        /// </summary>
        public bool InterceptDocuments
        {
            get { return interceptDocuments; }
            set
            {
                interceptDocuments = value;
                if (interceptDocuments == true && interceptedDocuments == null)
                {
                    interceptedDocuments = new Dictionary<string, XmlDocument>();
                }
                else if (interceptDocuments == false)
                {
                    interceptedDocuments = null;
                }
            }
        }

        /// <summary>
        /// Gets the intercepted documents if interceptDocuments is set to true
        /// </summary>
        public Dictionary<string, XmlDocument> InterceptedDocuments
        {
            get { return interceptedDocuments; }
        }

        #endregion

        #region constructors
        /// <summary>
        /// Constructor with defined workbook object
        /// </summary>
        /// <param name="workbook">Workbook to process</param>
        public LowLevel(Workbook workbook)
        {
            culture = CultureInfo.InvariantCulture;
            this.workbook = workbook;
            sharedStrings = new SortedMap();
            sharedStringsTotalCount = 0;
        }
        #endregion




        #region documentCreation_methods

        /// <summary>
        /// Method to create the app-properties (part of meta data) as raw XML string
        /// </summary>
        /// <returns>Raw XML string</returns>
        private string CreateAppPropertiesDocument()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">");
            sb.Append(CreateAppString());
            sb.Append("</Properties>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the core-properties (part of meta data) as raw XML string
        /// </summary>
        /// <returns>Raw XML string</returns>
        private string CreateCorePropertiesDocument()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            sb.Append(CreateCorePropertiesString());
            sb.Append("</cp:coreProperties>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create shared strings as raw XML string
        /// </summary>
        /// <returns>Raw XML string</returns>
        private string CreateSharedStringsDocument()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"");
            sb.Append(sharedStringsTotalCount.ToString("G", culture));
            sb.Append("\" uniqueCount=\"");
            sb.Append(sharedStrings.Count.ToString("G", culture));
            sb.Append("\">");
            foreach (string str in sharedStrings.Keys)
            {
                sb.Append("<si><t>");
                sb.Append(EscapeXmlChars(str));
                sb.Append("</t></si>");
            }
            sb.Append("</sst>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create a style sheet as raw XML string
        /// </summary>
        /// <returns>Raw XML string</returns>
        /// <exception cref="StyleException">Throws an StyleException if one of the styles cannot be referenced or is null</exception>
        /// <remarks>The UndefinedStyleException should never happen in this state if the internally managed style collection was not tampered. </remarks>
        private string CreateStyleSheetDocument()
        {
            string bordersString = CreateStyleBorderString();
            string fillsString = CreateStyleFillString();
            string fontsString = CreateStyleFontString();
            string numberFormatsString = CreateStyleNumberFormatString();
            string xfsStings = CreateStyleXfsString();
            string mruColorString = CreateMruColorsString();
            int fontCount = workbook.Styles.GetFontStyleNumber();
            int fillCount = workbook.Styles.GetFillStyleNumber();
            int styleCount = workbook.Styles.GetStyleNumber();
            int borderCount = workbook.Styles.GetBorderStyleNumber();
            StringBuilder sb = new StringBuilder();
            sb.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"x14ac\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\">");
            int numFormatCount = workbook.Styles.GetNumberFormatStyleNumber();
            if (numFormatCount > 0)
            {
                sb.Append("<numFmts count=\"").Append(numFormatCount.ToString("G", culture)).Append("\">");
                sb.Append(numberFormatsString + "</numFmts>");
            }
            sb.Append("<fonts x14ac:knownFonts=\"1\" count=\"").Append(fontCount.ToString("G", culture)).Append("\">");
            sb.Append(fontsString).Append("</fonts>");
            sb.Append("<fills count=\"").Append(fillCount.ToString("G", culture)).Append("\">");
            sb.Append(fillsString).Append("</fills>");
            sb.Append("<borders count=\"").Append(borderCount.ToString("G", culture)).Append("\">");
            sb.Append(bordersString).Append("</borders>");
            sb.Append("<cellXfs count=\"").Append(styleCount.ToString("G", culture)).Append("\">");
            sb.Append(xfsStings).Append("</cellXfs>");
            if (workbook.WorkbookMetadata != null)
            {
                if (string.IsNullOrEmpty(mruColorString) == false && workbook.WorkbookMetadata.UseColorMRU == true)
                {
                    sb.Append("<colors>");
                    sb.Append(mruColorString);
                    sb.Append("</colors>");
                }
            }
            sb.Append("</styleSheet>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create a workbook as raw XML string
        /// </summary>
        /// <returns>Raw XML string</returns>
        /// <exception cref="RangeException">Throws an OutOfRangeException if an address was out of range</exception>
        private string CreateWorkbookDocument()
        {
            if (workbook.Worksheets.Count == 0)
            {
                throw new RangeException("OutOfRangeException", "The workbook can not be created because no worksheet was defined.");
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            if (workbook.SelectedWorksheet > 0)
            {
                sb.Append("<bookViews><workbookView activeTab=\"");
                sb.Append(workbook.SelectedWorksheet.ToString("G", culture));
                sb.Append("\"/></bookViews>");
            }
            if (workbook.UseWorkbookProtection == true)
            {
                sb.Append("<workbookProtection");
                if (workbook.LockWindowsIfProtected == true)
                {
                    sb.Append(" lockWindows=\"1\"");
                }
                if (workbook.LockStructureIfProtected == true)
                {
                    sb.Append(" lockStructure=\"1\"");
                }
                if (string.IsNullOrEmpty(workbook.WorkbookProtectionPassword) == false)
                {
                    sb.Append("workbookPassword=\"");
                    sb.Append(GeneratePasswordHash(workbook.WorkbookProtectionPassword));
                    sb.Append("\"");
                }
                sb.Append("/>");
            }
            sb.Append("<sheets>");
            foreach (Worksheet item in workbook.Worksheets)
            {
                sb.Append("<sheet r:id=\"rId").Append(item.SheetID.ToString()).Append("\" sheetId=\"").Append(item.SheetID.ToString()).Append("\" name=\"").Append(EscapeXmlAttributeChars(item.SheetName)).Append("\"/>");
            }
            sb.Append("</sheets>");
            sb.Append("</workbook>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create a worksheet part as a raw XML string
        /// </summary>
        /// <param name="worksheet">worksheet object to process</param>
        /// <returns>Raw XML string</returns>
        /// <exception cref="FormatException">Throws a FormatException if a handled date cannot be translated to (Excel internal) OADate</exception>
        private string CreateWorksheetPart(Worksheet worksheet)
        {
            worksheet.RecalculateAutoFilter();
            worksheet.RecalculateColumns();
            List<List<Cell>> celldata = GetSortedSheetData(worksheet);
            StringBuilder sb = new StringBuilder();
            string line;
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"x14ac\" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\">");

            if (worksheet.SelectedCells != null)
            {
                sb.Append("<sheetViews><sheetView workbookViewId=\"0\"");
                if (workbook.SelectedWorksheet == worksheet.SheetID - 1)
                {
                    sb.Append(" tabSelected=\"1\"");
                }
                sb.Append("><selection sqref=\"");
                sb.Append(worksheet.SelectedCells.ToString());
                sb.Append("\" activeCell=\"");
                sb.Append(worksheet.SelectedCells.Value.StartAddress.ToString());
                sb.Append("\"/></sheetView></sheetViews>");
            }

            sb.Append("<sheetFormatPr x14ac:dyDescent=\"0.25\" defaultRowHeight=\"").Append(worksheet.DefaultRowHeight.ToString("G", culture)).Append("\" baseColWidth=\"").Append(worksheet.DefaultColumnWidth.ToString("G", culture)).Append("\"/>");

            string colWidths = CreateColsString(worksheet);
            if (string.IsNullOrEmpty(colWidths) == false)
            {
                sb.Append("<cols>");
                sb.Append(colWidths);
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            foreach (List<Cell> item in celldata)
            {
                line = CreateRowString(item, worksheet);
                sb.Append(line);
            }
            sb.Append("</sheetData>");
            sb.Append(CreateMergedCellsString(worksheet));
            sb.Append(CreateSheetProtectionString(worksheet));

            if (worksheet.AutoFilterRange != null)
            {
                sb.Append("<autoFilter ref=\"").Append(worksheet.AutoFilterRange.Value.ToString()).Append("\"/>");
            }

            sb.Append("</worksheet>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to save the workbook
        /// </summary>
        /// <exception cref="IOException">Throws IOException in case of an error</exception>
        /// <exception cref="RangeException">Throws an OutOfRangeException if the start or end address of a handled cell range was out of range</exception>
        /// <exception cref="FormatException">Throws a FormatException if a handled date cannot be translated to (Excel internal) OADate</exception>
        /// <exception cref="StyleException">Throws an StyleException if one of the styles of the workbook cannot be referenced or is null</exception>
        /// <remarks>The StyleException should never happen in this state if the internally managed style collection was not tampered. </remarks>
        public void Save()
        {
            try
            {
                FileStream fs = new FileStream(workbook.Filename, FileMode.Create);
                SaveAsStream(fs);

            }
            catch (Exception e)
            {
                throw new IOException("SaveException", "An error occurred while saving. See inner exception for details: " + e.Message, e);
            }
        }

        /// <summary>
        /// Method to save the workbook asynchronous.
        /// </summary>
        /// <remarks>Possible Exceptions are <see cref="IOException">IOException</see>, <see cref="RangeException">RangeException</see>, <see cref="FormatException"></see> and <see cref="StyleException">StyleException</see>. These exceptions may not emerge directly if using the async method since async/await adds further abstraction layers.</remarks>
        /// <returns>Async Task</returns>
        public async Task SaveAsync()
        {
            await Task.Run(() => { Save(); });
        }

        /// <summary>
        /// Method to save the workbook as stream
        /// </summary>
        /// <param name="stream">Writable stream as target</param>
        /// <param name="leaveOpen">Optional parameter to keep the stream open after writing (used for MemoryStreams; default is false)</param>
        /// <exception cref="IOException">Throws IOException in case of an error</exception>
        /// <exception cref="RangeException">Throws an OutOfRangeException if the start or end address of a handled cell range was out of range</exception>
        /// <exception cref="FormatException">Throws a FormatException if a handled date cannot be translated to (Excel internal) OADate</exception>
        /// <exception cref="StyleException">Throws an StyleException if one of the styles of the workbook cannot be referenced or is null</exception>
        /// <remarks>The StyleException should never happen in this state if the internally managed style collection was not tampered. </remarks>
        public void SaveAsStream(Stream stream, bool leaveOpen = false)
        {
            workbook.ResolveMergedCells();
            DocumentPath sheetPath;
            List<Uri> sheetURIs = new List<Uri>();
            try
            {
                using (Package p = Package.Open(stream, FileMode.Create))
                {
                    Uri workbookUri = new Uri(WORKBOOK.GetFullPath(), UriKind.Relative);
                    Uri stylesheetUri = new Uri(STYLES.GetFullPath(), UriKind.Relative);
                    Uri appPropertiesUri = new Uri(APP_PROPERTIES.GetFullPath(), UriKind.Relative);
                    Uri corePropertiesUri = new Uri(CORE_PROPERTIES.GetFullPath(), UriKind.Relative);
                    Uri sharedStringsUri = new Uri(SHARED_STRINGS.GetFullPath(), UriKind.Relative);

                    PackagePart pp = p.CreatePart(workbookUri, @"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", CompressionOption.Normal);
                    p.CreateRelationship(pp.Uri, TargetMode.Internal, @"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "rId1");
                    p.CreateRelationship(corePropertiesUri, TargetMode.Internal, @"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "rId2"); //!
                    p.CreateRelationship(appPropertiesUri, TargetMode.Internal, @"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "rId3"); //!

                    AppendXmlToPackagePart(CreateWorkbookDocument(), pp, "WORKBOOK");
                    int idCounter = workbook.Worksheets.Count + 1;

                    pp.CreateRelationship(stylesheetUri, TargetMode.Internal, @"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "rId" + idCounter.ToString());
                    pp.CreateRelationship(sharedStringsUri, TargetMode.Internal, @"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings", "rId" + (idCounter + 1).ToString());

                    foreach (Worksheet item in workbook.Worksheets)
                    {
                        sheetPath = new DocumentPath("sheet" + item.SheetID.ToString() + ".xml", "xl/worksheets");
                        sheetURIs.Add(new Uri(sheetPath.GetFullPath(), UriKind.Relative));
                        pp.CreateRelationship(sheetURIs[sheetURIs.Count - 1], TargetMode.Internal, @"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "rId" + item.SheetID.ToString());
                    }

                    pp = p.CreatePart(stylesheetUri, @"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml", CompressionOption.Normal);
                    AppendXmlToPackagePart(CreateStyleSheetDocument(), pp, "STYLESHEET");

                    int i = 0;
                    foreach (Worksheet item in workbook.Worksheets)
                    {
                        pp = p.CreatePart(sheetURIs[i], @"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", CompressionOption.Normal);
                        i++;
                        AppendXmlToPackagePart(CreateWorksheetPart(item), pp, "WORKSHEET:" + item.SheetName);
                    }
                    pp = p.CreatePart(sharedStringsUri, @"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml", CompressionOption.Normal);
                    AppendXmlToPackagePart(CreateSharedStringsDocument(), pp, "SHAREDSTRINGS");
                    if (workbook.WorkbookMetadata != null)
                    {
                        pp = p.CreatePart(appPropertiesUri, @"application/vnd.openxmlformats-officedocument.extended-properties+xml", CompressionOption.Normal);
                        AppendXmlToPackagePart(CreateAppPropertiesDocument(), pp, "APPPROPERTIES");
                        pp = p.CreatePart(corePropertiesUri, @"application/vnd.openxmlformats-package.core-properties+xml", CompressionOption.Normal);
                        AppendXmlToPackagePart(CreateCorePropertiesDocument(), pp, "COREPROPERTIES");
                    }
                    p.Flush();
                    p.Close();
                    if (leaveOpen == false)
                    {
                        stream.Close();
                    }
                }
            }
            catch (Exception e)
            {
                throw new IOException("SaveException", "An error occurred while saving. See inner exception for details: " + e.Message, e);
            }
        }

        /// <summary>
        /// Method to save the workbook as stream asynchronous.
        /// </summary>
        /// <param name="stream">Writable stream as target</param>
        /// <param name="leaveOpen">Optional parameter to keep the stream open after writing (used for MemoryStreams; default is false)</param>
        /// <remarks>Possible Exceptions are <see cref="IOException">IOException</see>, <see cref="RangeException">RangeException</see>, <see cref="FormatException"></see> and <see cref="StyleException">StyleException</see>. These exceptions may not emerge directly if using the async method since async/await adds further abstraction layers.</remarks>
        /// <returns>Async Task</returns>
        public async Task SaveAsStreamAsync(Stream stream, bool leaveOpen = false)
        {
            await Task.Run(() => { SaveAsStream(stream, leaveOpen); });
        }

        #endregion

        #region documentUtil_methods

        /// <summary>
        /// Method to append a simple XML tag with an enclosed value to the passed StringBuilder
        /// </summary>
        /// <param name="sb">StringBuilder to append</param>
        /// <param name="value">Value of the XML element</param>
        /// <param name="tagName">Tag name of the XML element</param>
        /// <param name="nameSpace">Optional XML name space. Can be empty or null</param>
        /// <returns>Returns false if no tag was appended, because the value or tag name was null or empty</returns>
        private bool AppendXmlTag(StringBuilder sb, string value, string tagName, string nameSpace)
        {
            if (string.IsNullOrEmpty(value)) { return false; }
            if (sb == null || string.IsNullOrEmpty(tagName)) { return false; }
            bool hasNoNs = string.IsNullOrEmpty(nameSpace);
            sb.Append('<');
            if (hasNoNs == false)
            {
                sb.Append(nameSpace);
                sb.Append(':');
            }
            sb.Append(tagName).Append(">");
            sb.Append(EscapeXmlChars(value));
            sb.Append("</");
            if (hasNoNs == false)
            {
                sb.Append(nameSpace);
                sb.Append(':');
            }
            sb.Append(tagName);
            sb.Append('>');
            return true;
        }

        /// <summary>
        /// Writes raw XML strings into the passed Package Part
        /// </summary>
        /// <param name="doc">document as raw XML string</param>
        /// <param name="pp">Package part to append the XML data</param>
        /// <param name="title">Title for interception / debugging purpose</param>
        /// <exception cref="IOException">Throws an IOException if the XML data could not be written into the Package Part</exception>
        private void AppendXmlToPackagePart(string doc, PackagePart pp, string title)
        {
            try
            {
                if (interceptDocuments == true)
                {
                    XmlDocument xDoc = new XmlDocument();
                    xDoc.LoadXml(doc);
                    interceptedDocuments.Add(title, xDoc);
                }
                using (MemoryStream ms = new MemoryStream()) // Write workbook.xml
                {
                    if (ms.CanWrite == false) { return; }
                    using (XmlWriter writer = XmlWriter.Create(ms))
                    {
                        //doc.WriteTo(writer);
                        writer.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"");
                        writer.WriteRaw(doc);
                        writer.Flush();
                        ms.Position = 0;
                        ms.CopyTo(pp.GetStream());
                        ms.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                throw new IOException("MemoryStreamException", "The XML document could not be saved into the memory stream", e);
            }
        }

        /// <summary>
        /// Method to create the XML string for the app-properties document
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateAppString()
        {
            if (workbook.WorkbookMetadata == null) { return string.Empty; }
            Metadata md = workbook.WorkbookMetadata;
            StringBuilder sb = new StringBuilder();
            AppendXmlTag(sb, "0", "TotalTime", null);
            AppendXmlTag(sb, md.Application, "Application", null);
            AppendXmlTag(sb, "0", "DocSecurity", null);
            AppendXmlTag(sb, "false", "ScaleCrop", null);
            AppendXmlTag(sb, md.Manager, "Manager", null);
            AppendXmlTag(sb, md.Company, "Company", null);
            AppendXmlTag(sb, "false", "LinksUpToDate", null);
            AppendXmlTag(sb, "false", "SharedDoc", null);
            AppendXmlTag(sb, md.HyperlinkBase, "HyperlinkBase", null);
            AppendXmlTag(sb, "false", "HyperlinksChanged", null);
            AppendXmlTag(sb, md.ApplicationVersion, "AppVersion", null);
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the columns as XML string. This is used to define the width of columns
        /// </summary>
        /// <param name="worksheet">Worksheet to process</param>
        /// <returns>String with formatted XML data</returns>
        private string CreateColsString(Worksheet worksheet)
        {
            if (worksheet.Columns.Count > 0)
            {
                string col;
                string hidden = "";
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<int, Worksheet.Column> column in worksheet.Columns)
                {
                    if (column.Value.Width == worksheet.DefaultColumnWidth && column.Value.IsHidden == false) { continue; }
                    if (worksheet.Columns.ContainsKey(column.Key))
                    {
                        if (worksheet.Columns[column.Key].IsHidden == true)
                        {
                            hidden = " hidden=\"1\"";
                        }
                    }
                    col = (column.Key + 1).ToString("G", culture); // Add 1 for Address
                    sb.Append("<col customWidth=\"1\" width=\"").Append(column.Value.Width.ToString("G", culture)).Append("\" max=\"").Append(col).Append("\" min=\"").Append(col).Append("\"").Append(hidden).Append("/>");
                }
                string value = sb.ToString();
                if (value.Length > 0)
                {
                    return value;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Method to create the XML string for the core-properties document
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateCorePropertiesString()
        {
            if (workbook.WorkbookMetadata == null) { return string.Empty; }
            Metadata md = workbook.WorkbookMetadata;
            StringBuilder sb = new StringBuilder();
            AppendXmlTag(sb, md.Title, "title", "dc");
            AppendXmlTag(sb, md.Subject, "subject", "dc");
            AppendXmlTag(sb, md.Creator, "creator", "dc");
            AppendXmlTag(sb, md.Creator, "lastModifiedBy", "cp");
            AppendXmlTag(sb, md.Keywords, "keywords", "cp");
            AppendXmlTag(sb, md.Description, "description", "dc");
            string time = DateTime.Now.ToString("yyyy-MM-ddThh:mm:ssZ", culture);
            sb.Append("<dcterms:created xsi:type=\"dcterms:W3CDTF\">").Append(time).Append("</dcterms:created>");
            sb.Append("<dcterms:modified xsi:type=\"dcterms:W3CDTF\">").Append(time).Append("</dcterms:modified>");

            AppendXmlTag(sb, md.Category, "category", "cp");
            AppendXmlTag(sb, md.ContentStatus, "contentStatus", "cp");

            return sb.ToString();
        }

        /// <summary>
        /// Method to create the merged cells string of the passed worksheet
        /// </summary>
        /// <param name="sheet">Worksheet to process</param>
        /// <returns>Formatted string with merged cell ranges</returns>
        private string CreateMergedCellsString(Worksheet sheet)
        {
            if (sheet.MergedCells.Count < 1)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<mergeCells count=\"").Append(sheet.MergedCells.Count.ToString("G", culture)).Append("\">");
            foreach (KeyValuePair<string, Cell.Range> item in sheet.MergedCells)
            {
                sb.Append("<mergeCell ref=\"").Append(item.Value.ToString()).Append("\"/>");
            }
            sb.Append("</mergeCells>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create a row string
        /// </summary>
        /// <param name="columnFields">List of cells</param>
        /// <param name="worksheet">Worksheet to process</param>
        /// <returns>Formatted row string</returns>
        /// <exception cref="FormatException">Throws a FormatException if a handled date cannot be translated to (Excel internal) OADate</exception>
        private string CreateRowString(List<Cell> columnFields, Worksheet worksheet)
        {
            int rowNumber = columnFields[0].RowNumber;
            string height = "";
            string hidden = "";
            if (worksheet.RowHeights.ContainsKey(rowNumber))
            {
                if (worksheet.RowHeights[rowNumber] != worksheet.DefaultRowHeight)
                {
                    height = " x14ac:dyDescent=\"0.25\" customHeight=\"1\" ht=\"" + worksheet.RowHeights[rowNumber].ToString("G", culture) + "\"";
                }
            }
            if (worksheet.HiddenRows.ContainsKey(rowNumber))
            {
                if (worksheet.HiddenRows[rowNumber] == true)
                {
                    hidden = " hidden=\"1\"";
                }
            }
            StringBuilder sb = new StringBuilder();
            if (columnFields.Count > 0)
            {
                sb.Append("<row r=\"").Append((rowNumber + 1).ToString()).Append("\"").Append(height).Append(hidden).Append(">");
            }
            else
            {
                sb.Append("<row").Append(height).Append(">");
            }
            string typeAttribute;
            string sValue = "";
            string tValue = "";
            string value = "";
            bool bVal;

            DateTime dVal;
            int col = 0;
            foreach (Cell item in columnFields)
            {
                tValue = " ";
                if (item.CellStyle != null)
                {
                    sValue = " s=\"" + item.CellStyle.InternalID.Value.ToString("G", culture) + "\" ";
                }
                else
                {
                    sValue = "";
                }
                item.ResolveCellType(); // Recalculate the type (for handling DEFAULT)
                if (item.DataType == Cell.CellType.BOOL)
                {
                    typeAttribute = "b";
                    tValue = " t=\"" + typeAttribute + "\" ";
                    bVal = (bool)item.Value;
                    if (bVal == true) { value = "1"; }
                    else { value = "0"; }

                }
                // Number casting
                else if (item.DataType == Cell.CellType.NUMBER)
                {
                    typeAttribute = "n";
                    tValue = " t=\"" + typeAttribute + "\" ";
                    Type t = item.Value.GetType();

                    if (t == typeof(byte))         { value = ((byte)item.Value).ToString("G", culture); }
                    else if (t == typeof(sbyte))   { value = ((sbyte)item.Value).ToString("G", culture); }
                    else if (t == typeof(decimal)) { value = ((decimal)item.Value).ToString("G", culture); }
                    else if (t == typeof(double))  { value = ((double)item.Value).ToString("G", culture); }
                    else if (t == typeof(float))   { value = ((float)item.Value).ToString("G", culture); }
                    else if (t == typeof(int))     { value = ((int)item.Value).ToString("G", culture); }
                    else if (t == typeof(uint))    { value = ((uint)item.Value).ToString("G", culture); }
                    else if (t == typeof(long))    { value = ((long)item.Value).ToString("G", culture); }
                    else if (t == typeof(ulong))   { value = ((ulong)item.Value).ToString("G", culture); }
                    else if (t == typeof(short))   { value = ((short)item.Value).ToString("G", culture); }
                    else if (t == typeof(ushort))  { value = ((ushort)item.Value).ToString("G", culture); }
                 }
                // Date parsing
                else if (item.DataType == Cell.CellType.DATE)
                {
                    typeAttribute = "d";
                    dVal = (DateTime)item.Value;
                    value = GetOADateTimeString(dVal, culture);
                }
                else
                {
                    if (item.Value == null)
                    {
                        typeAttribute = "str";
                        value = string.Empty;
                    }
                    else // Handle sharedStrings
                    {
                        if (item.DataType == Cell.CellType.FORMULA)
                        {
                            typeAttribute = "str";
                            value = item.Value.ToString();
                        }
                        else
                        {
                            typeAttribute = "s";
                            value = item.Value.ToString();
                            if (sharedStrings.ContainsKey(value) == false)
                            {
                                sharedStrings.Add(value, sharedStrings.Count.ToString("G", culture));
                            }
                            value = sharedStrings[value];
                            sharedStringsTotalCount++;
                        }
                    }
                    tValue = " t=\"" + typeAttribute + "\" ";
                }
                if (item.DataType != Cell.CellType.EMPTY)
                {
                    sb.Append("<c").Append(tValue).Append("r=\"").Append(item.CellAddress).Append("\"").Append(sValue).Append(">");
                    if (item.DataType == Cell.CellType.FORMULA)
                    {
                        sb.Append("<f>").Append(EscapeXmlChars(item.Value.ToString())).Append("</f>");
                    }
                    else
                    {
                        sb.Append("<v>").Append(EscapeXmlChars(value)).Append("</v>");
                    }
                    sb.Append("</c>");
                }
                else // Empty cell
                {
                    sb.Append("<c").Append(tValue).Append("r=\"").Append(item.CellAddress).Append("\"").Append(sValue).Append("/>");
                }
                col++;
            }
            sb.Append("</row>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the protection string of the passed worksheet
        /// </summary>
        /// <param name="sheet">Worksheet to process</param>
        /// <returns>Formatted string with protection statement of the worksheet</returns>
        private string CreateSheetProtectionString(Worksheet sheet)
        {
            if (sheet.UseSheetProtection == false)
            {
                return string.Empty;
            }
            Dictionary<Worksheet.SheetProtectionValue, int> actualLockingValues = new Dictionary<Worksheet.SheetProtectionValue, int>();
            if (sheet.SheetProtectionValues.Count == 0)
            {
                actualLockingValues.Add(Worksheet.SheetProtectionValue.selectLockedCells, 1);
                actualLockingValues.Add(Worksheet.SheetProtectionValue.selectUnlockedCells, 1);
            }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.objects) == false)
            {
                actualLockingValues.Add(Worksheet.SheetProtectionValue.objects, 1);
            }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.scenarios) == false)
            {
                actualLockingValues.Add(Worksheet.SheetProtectionValue.scenarios, 1);
            }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.selectLockedCells) == false)
            {
                if (actualLockingValues.ContainsKey(Worksheet.SheetProtectionValue.selectLockedCells) == false)
                {
                    actualLockingValues.Add(Worksheet.SheetProtectionValue.selectLockedCells, 1);
                }
            }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.selectUnlockedCells) == false || sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.selectLockedCells) == false)
            {
                if (actualLockingValues.ContainsKey(Worksheet.SheetProtectionValue.selectUnlockedCells) == false)
                {
                    actualLockingValues.Add(Worksheet.SheetProtectionValue.selectUnlockedCells, 1);
                }
            }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.formatCells)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.formatCells, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.formatColumns)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.formatColumns, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.formatRows)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.formatRows, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.insertColumns)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.insertColumns, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.insertRows)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.insertRows, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.insertHyperlinks)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.insertHyperlinks, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.deleteColumns)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.deleteColumns, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.deleteRows)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.deleteRows, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.sort)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.sort, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.autoFilter)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.autoFilter, 0); }
            if (sheet.SheetProtectionValues.Contains(Worksheet.SheetProtectionValue.pivotTables)) { actualLockingValues.Add(Worksheet.SheetProtectionValue.pivotTables, 0); }
            StringBuilder sb = new StringBuilder();
            sb.Append("<sheetProtection");
            string temp;
            foreach (KeyValuePair<Worksheet.SheetProtectionValue, int> item in actualLockingValues)
            {
                try
                {
                    temp = Enum.GetName(typeof(Worksheet.SheetProtectionValue), item.Key); // Note! If the enum names differs from the OOXML definitions, this method will cause invalid OOXML entries
                    sb.Append(" ").Append(temp).Append("=\"").Append(item.Value.ToString("G", culture)).Append("\"");
                }
                catch { }
            }
            if (string.IsNullOrEmpty(sheet.SheetProtectionPassword) == false)
            {
                string hash = GeneratePasswordHash(sheet.SheetProtectionPassword);
                sb.Append(" password=\"").Append(hash).Append("\"");
            }
            sb.Append(" sheet=\"1\"/>");
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the XML string for the border part of the style sheet document
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateStyleBorderString()
        {
            Style.Border[] borderStyles = workbook.Styles.GetBorders();
            StringBuilder sb = new StringBuilder();
            foreach (Style.Border item in borderStyles)
            {
                if (item.DiagonalDown == true && item.DiagonalUp == false) { sb.Append("<border diagonalDown=\"1\">"); }
                else if (item.DiagonalDown == false && item.DiagonalUp == true) { sb.Append("<border diagonalUp=\"1\">"); }
                else if (item.DiagonalDown == true && item.DiagonalUp == true) { sb.Append("<border diagonalDown=\"1\" diagonalUp=\"1\">"); }
                else { sb.Append("<border>"); }

                if (item.LeftStyle != Style.Border.StyleValue.none)
                {
                    sb.Append("<left style=\"" + Style.Border.GetStyleName(item.LeftStyle) + "\">");
                    if (string.IsNullOrEmpty(item.LeftColor) == true) { sb.Append("<color rgb=\"").Append(item.LeftColor).Append("\"/>"); }
                    else { sb.Append("<color auto=\"1\"/>"); }
                    sb.Append("</left>");
                }
                else
                {
                    sb.Append("<left/>");
                }
                if (item.RightStyle != Style.Border.StyleValue.none)
                {
                    sb.Append("<right style=\"").Append(Style.Border.GetStyleName(item.RightStyle)).Append("\">");
                    if (string.IsNullOrEmpty(item.RightColor) == true) { sb.Append("<color rgb=\"").Append(item.RightColor).Append("\"/>"); }
                    else { sb.Append("<color auto=\"1\"/>"); }
                    sb.Append("</right>");
                }
                else
                {
                    sb.Append("<right/>");
                }
                if (item.TopStyle != Style.Border.StyleValue.none)
                {
                    sb.Append("<top style=\"").Append(Style.Border.GetStyleName(item.TopStyle)).Append("\">");
                    if (string.IsNullOrEmpty(item.TopColor) == true) { sb.Append("<color rgb=\"").Append(item.TopColor).Append("\"/>"); }
                    else { sb.Append("<color auto=\"1\"/>"); }
                    sb.Append("</top>");
                }
                else
                {
                    sb.Append("<top/>");
                }
                if (item.BottomStyle != Style.Border.StyleValue.none)
                {
                    sb.Append("<bottom style=\"").Append(Style.Border.GetStyleName(item.BottomStyle)).Append("\">");
                    if (string.IsNullOrEmpty(item.BottomColor) == true) { sb.Append("<color rgb=\"").Append(item.BottomColor).Append("\"/>"); }
                    else { sb.Append("<color auto=\"1\"/>"); }
                    sb.Append("</bottom>");
                }
                else
                {
                    sb.Append("<bottom/>");
                }
                if (item.DiagonalStyle != Style.Border.StyleValue.none)
                {
                    sb.Append("<diagonal style=\"").Append(Style.Border.GetStyleName(item.DiagonalStyle)).Append("\">");
                    if (string.IsNullOrEmpty(item.DiagonalColor) == true) { sb.Append("<color rgb=\"").Append(item.DiagonalColor).Append("\"/>"); }
                    else { sb.Append("<color auto=\"1\"/>"); }
                    sb.Append("</diagonal>");
                }
                else
                {
                    sb.Append("<diagonal/>");
                }

                sb.Append("</border>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the XML string for the font part of the style sheet document
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateStyleFontString()
        {
            Style.Font[] fontStyles = workbook.Styles.GetFonts();
            StringBuilder sb = new StringBuilder();
            foreach (Style.Font item in fontStyles)
            {
                sb.Append("<font>");
                if (item.Bold == true) { sb.Append("<b/>"); }
                if (item.Italic == true) { sb.Append("<i/>"); }
                if (item.Underline == true) { sb.Append("<u/>"); }
                if (item.DoubleUnderline == true) { sb.Append("<u val=\"double\"/>"); }
                if (item.Strike == true) { sb.Append("<strike/>"); }
                if (item.VerticalAlign == Style.Font.VerticalAlignValue.subscript) { sb.Append("<vertAlign val=\"subscript\"/>"); }
                else if (item.VerticalAlign == Style.Font.VerticalAlignValue.superscript) { sb.Append("<vertAlign val=\"superscript\"/>"); }
                sb.Append("<sz val=\"").Append(item.Size.ToString("G", culture)).Append("\"/>");
                if (string.IsNullOrEmpty(item.ColorValue))
                {
                    sb.Append("<color theme=\"").Append(item.ColorTheme.ToString("G", culture)).Append("\"/>");
                }
                else
                {
                    sb.Append("<color rgb=\"").Append(item.ColorValue).Append("\"/>");
                }
                sb.Append("<name val=\"").Append(item.Name).Append("\"/>");
                sb.Append("<family val=\"").Append(item.Family).Append("\"/>");
                if (item.Scheme != Style.Font.SchemeValue.none)
                {
                    if (item.Scheme == Style.Font.SchemeValue.major)
                    { sb.Append("<scheme val=\"major\"/>"); }
                    else if (item.Scheme == Style.Font.SchemeValue.minor)
                    { sb.Append("<scheme val=\"minor\"/>"); }
                }
                if (string.IsNullOrEmpty(item.Charset) == false)
                {
                    sb.Append("<charset val=\"").Append(item.Charset).Append("\"/>");
                }
                sb.Append("</font>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the XML string for the fill part of the style sheet document
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateStyleFillString()
        {
            Style.Fill[] fillStyles = workbook.Styles.GetFills();
            StringBuilder sb = new StringBuilder();
            foreach (Style.Fill item in fillStyles)
            {
                sb.Append("<fill>");
                sb.Append("<patternFill patternType=\"").Append(Style.Fill.GetPatternName(item.PatternFill)).Append("\"");
                if (item.PatternFill == Style.Fill.PatternValue.solid)
                {
                    sb.Append(">");
                    sb.Append("<fgColor rgb=\"").Append(item.ForegroundColor).Append("\"/>");
                    sb.Append("<bgColor indexed=\"").Append(item.IndexedColor.ToString("G", culture)).Append("\"/>");
                    sb.Append("</patternFill>");
                }
                else if (item.PatternFill == Style.Fill.PatternValue.mediumGray || item.PatternFill == Style.Fill.PatternValue.lightGray || item.PatternFill == Style.Fill.PatternValue.gray0625 || item.PatternFill == Style.Fill.PatternValue.darkGray)
                {
                    sb.Append(">");
                    sb.Append("<fgColor rgb=\"").Append(item.ForegroundColor).Append("\"/>");
                    if (string.IsNullOrEmpty(item.BackgroundColor) == false)
                    {
                        sb.Append("<bgColor rgb=\"").Append(item.BackgroundColor).Append("\"/>");
                    }
                    sb.Append("</patternFill>");
                }
                else
                {
                    sb.Append("/>");
                }
                sb.Append("</fill>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the XML string for the number format part of the style sheet document 
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateStyleNumberFormatString()
        {
            Style.NumberFormat[] numberFormatStyles = workbook.Styles.GetNumberFormats();
            StringBuilder sb = new StringBuilder();
            foreach (Style.NumberFormat item in numberFormatStyles)
            {
                if (item.IsCustomFormat == true)
                {
                    sb.Append("<numFmt formatCode=\"").Append(item.CustomFormatCode).Append("\" numFmtId=\"").Append(item.CustomFormatID.ToString("G", culture)).Append("\"/>");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the XML string for the Xf part of the style sheet document
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateStyleXfsString()
        {
            Style[] styles = workbook.Styles.GetStyles();
            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();
            string alignmentString, protectionString;
            int formatNumber, textRotation;
            foreach (Style item in styles)
            {
                textRotation = item.CurrentCellXf.CalculateInternalRotation();
                alignmentString = string.Empty;
                protectionString = string.Empty;
                if (item.CurrentCellXf.HorizontalAlign != Style.CellXf.HorizontalAlignValue.none || item.CurrentCellXf.VerticalAlign != Style.CellXf.VerticalAlignValue.none || item.CurrentCellXf.Alignment != Style.CellXf.TextBreakValue.none || textRotation != 0)
                {
                    sb2.Clear();
                    sb2.Append("<alignment");
                    if (item.CurrentCellXf.HorizontalAlign != Style.CellXf.HorizontalAlignValue.none)
                    {
                        sb2.Append(" horizontal=\"");
                        if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.center) { sb2.Append("center"); }
                        else if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.right) { sb2.Append("right"); }
                        else if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.centerContinuous) { sb2.Append("centerContinuous"); }
                        else if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.distributed) { sb2.Append("distributed"); }
                        else if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.fill) { sb2.Append("fill"); }
                        else if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.general) { sb2.Append("general"); }
                        else if (item.CurrentCellXf.HorizontalAlign == Style.CellXf.HorizontalAlignValue.justify) { sb2.Append("justify"); }
                        else { sb2.Append("left"); }
                        sb2.Append("\"");
                    }
                    if (item.CurrentCellXf.VerticalAlign != Style.CellXf.VerticalAlignValue.none)
                    {
                        sb2.Append(" vertical=\"");
                        if (item.CurrentCellXf.VerticalAlign == Style.CellXf.VerticalAlignValue.center) { sb2.Append("center"); }
                        else if (item.CurrentCellXf.VerticalAlign == Style.CellXf.VerticalAlignValue.distributed) { sb2.Append("distributed"); }
                        else if (item.CurrentCellXf.VerticalAlign == Style.CellXf.VerticalAlignValue.justify) { sb2.Append("justify"); }
                        else if (item.CurrentCellXf.VerticalAlign == Style.CellXf.VerticalAlignValue.top) { sb2.Append("top"); }
                        else { sb2.Append("bottom"); }
                        sb2.Append("\"");
                    }

                    if (item.CurrentCellXf.Alignment != Style.CellXf.TextBreakValue.none)
                    {
                        if (item.CurrentCellXf.Alignment == Style.CellXf.TextBreakValue.shrinkToFit) { sb2.Append(" shrinkToFit=\"1"); }
                        else { sb2.Append(" wrapText=\"1"); }
                        sb2.Append("\"");
                    }
                    if (textRotation != 0)
                    {
                        sb2.Append(" textRotation=\"");
                        sb2.Append(textRotation.ToString("G", culture));
                        sb2.Append("\"");
                    }
                    sb2.Append("/>"); // </xf>
                    alignmentString = sb2.ToString();
                }

                if (item.CurrentCellXf.Hidden == true || item.CurrentCellXf.Locked == true)
                {
                    if (item.CurrentCellXf.Hidden == true && item.CurrentCellXf.Locked == true)
                    {
                        protectionString = "<protection locked=\"1\" hidden=\"1\"/>";
                    }
                    else if (item.CurrentCellXf.Hidden == true && item.CurrentCellXf.Locked == false)
                    {
                        protectionString = "<protection hidden=\"1\" locked=\"0\"/>";
                    }
                    else
                    {
                        protectionString = "<protection hidden=\"0\" locked=\"1\"/>";
                    }
                }

                sb.Append("<xf numFmtId=\"");
                if (item.CurrentNumberFormat.IsCustomFormat == true)
                {
                    sb.Append(item.CurrentNumberFormat.CustomFormatID.ToString("G", culture));
                }
                else
                {
                    formatNumber = (int)item.CurrentNumberFormat.Number;
                    sb.Append(formatNumber.ToString("G", culture));
                }
                sb.Append("\" borderId=\"").Append(item.CurrentBorder.InternalID.Value.ToString("G", culture));
                sb.Append("\" fillId=\"").Append(item.CurrentFill.InternalID.Value.ToString("G", culture));
                sb.Append("\" fontId=\"").Append(item.CurrentFont.InternalID.Value.ToString("G", culture));
                if (item.CurrentFont.IsDefaultFont == false)
                {
                    sb.Append("\" applyFont=\"1");
                }
                if (item.CurrentFill.PatternFill != Style.Fill.PatternValue.none)
                {
                    sb.Append("\" applyFill=\"1");
                }
                if (item.CurrentBorder.IsEmpty() == false)
                {
                    sb.Append("\" applyBorder=\"1");
                }
                if (alignmentString != string.Empty || item.CurrentCellXf.ForceApplyAlignment == true)
                {
                    sb.Append("\" applyAlignment=\"1");
                }
                if (protectionString != string.Empty)
                {
                    sb.Append("\" applyProtection=\"1");
                }
                if (item.CurrentNumberFormat.Number != Style.NumberFormat.FormatNumber.none)
                {
                    sb.Append("\" applyNumberFormat=\"1\"");
                }
                else
                {
                    sb.Append("\"");
                }
                if (alignmentString != string.Empty || protectionString != string.Empty)
                {
                    sb.Append(">");
                    sb.Append(alignmentString);
                    sb.Append(protectionString);
                    sb.Append("</xf>");
                }
                else
                {
                    sb.Append("/>");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Method to create the XML string for the color-MRU part of the style sheet document (recent colors)
        /// </summary>
        /// <returns>String with formatted XML data</returns>
        private string CreateMruColorsString()
        {
            Style.Font[] fonts = workbook.Styles.GetFonts();
            Style.Fill[] fills = workbook.Styles.GetFills();
            StringBuilder sb = new StringBuilder();
            List<string> tempColors = new List<string>();
            foreach (Style.Font item in fonts)
            {
                if (string.IsNullOrEmpty(item.ColorValue) == true) { continue; }
                if (item.ColorValue == Style.Fill.DEFAULTCOLOR) { continue; }
                if (tempColors.Contains(item.ColorValue) == false) { tempColors.Add(item.ColorValue); }
            }
            foreach (Style.Fill item in fills)
            {
                if (string.IsNullOrEmpty(item.BackgroundColor) == false)
                {
                    if (item.BackgroundColor != Style.Fill.DEFAULTCOLOR)
                    {
                        if (tempColors.Contains(item.BackgroundColor) == false) { tempColors.Add(item.BackgroundColor); }
                    }
                }
                if (string.IsNullOrEmpty(item.ForegroundColor) == false)
                {
                    if (item.ForegroundColor != Style.Fill.DEFAULTCOLOR)
                    {
                        if (tempColors.Contains(item.ForegroundColor) == false) { tempColors.Add(item.ForegroundColor); }
                    }
                }
            }
            if (tempColors.Count > 0)
            {
                sb.Append("<mruColors>");
                foreach (string item in tempColors)
                {
                    sb.Append("<color rgb=\"").Append(item).Append("\"/>");
                }
                sb.Append("</mruColors>");
                return sb.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Method to sort the cells of a worksheet as preparation for the XML document
        /// </summary>
        /// <param name="sheet">Worksheet to process</param>
        /// <returns>Two dimensional array of Cell objects</returns>
        private List<List<Cell>> GetSortedSheetData(Worksheet sheet)
        {
            List<Cell> temp = new List<Cell>();
            foreach (KeyValuePair<string, Cell> item in sheet.Cells)
            {
                temp.Add(item.Value);
            }
            temp.Sort();
            List<Cell> line = new List<Cell>();
            List<List<Cell>> output = new List<List<Cell>>();
            if (temp.Count > 0)
            {
                int rowNumber = temp[0].RowNumber;
                foreach (Cell item in temp)
                {
                    if (item.RowNumber != rowNumber)
                    {
                        output.Add(line);
                        line = new List<Cell>();
                        rowNumber = item.RowNumber;
                    }
                    line.Add(item);
                }
                if (line.Count > 0)
                {
                    output.Add(line);
                }
            }
            return output;
        }


        #endregion

        #region staticMethods

        /// <summary>
        /// Method to escape XML characters between two XML tags
        /// </summary>
        /// <param name="input">Input string to process</param>
        /// <returns>Escaped string</returns>
        /// <remarks>Note: The XML specs allow characters up to the character value of 0x10FFFF. However, the C# char range is only up to 0xFFFF. PicoXLSX will neglect all values above this level in the sanitizing check. Illegal characters like 0x1 will be replaced with a white space (0x20)</remarks>
        public static string EscapeXmlChars(string input)
        {
            if (input == null) { return ""; }
            int len = input.Length;
            List<int> illegalCharacters = new List<int>(len);
            List<byte> characterTypes = new List<byte>(len);
            int i;
            for (i = 0; i < len; i++)
            {
                if ((input[i] < 0x9) || (input[i] > 0xA && input[i] < 0xD) || (input[i] > 0xD && input[i] < 0x20) || (input[i] > 0xD7FF && input[i] < 0xE000) || (input[i] > 0xFFFD))
                {
                    illegalCharacters.Add(i);
                    characterTypes.Add(0);
                    continue;
                } // Note: XML specs allow characters up to 0x10FFFF. However, the C# char range is only up to 0xFFFF; Higher values are neglected here 
                if (input[i] == 0x3C) // <
                {
                    illegalCharacters.Add(i);
                    characterTypes.Add(1);
                }
                else if (input[i] == 0x3E) // >
                {
                    illegalCharacters.Add(i);
                    characterTypes.Add(2);
                }
                else if (input[i] == 0x26) // &
                {
                    illegalCharacters.Add(i);
                    characterTypes.Add(3);
                }
            }
            if (illegalCharacters.Count == 0)
            {
                return input;
            }

            StringBuilder sb = new StringBuilder(len);
            int lastIndex = 0;
            len = illegalCharacters.Count;
            for (i = 0; i < len; i++)
            {
                sb.Append(input.Substring(lastIndex, illegalCharacters[i] - lastIndex));
                if (characterTypes[i] == 0)
                {
                    sb.Append(' '); // Whitespace as fall back on illegal character
                }
                else if (characterTypes[i] == 1) // replace <
                {
                    sb.Append("&lt;");
                }
                else if (characterTypes[i] == 2) // replace >
                {
                    sb.Append("&gt;");
                }
                else if (characterTypes[i] == 3) // replace &
                {
                    sb.Append("&amp;");
                }
                lastIndex = illegalCharacters[i] + 1;
            }
            sb.Append(input.Substring(lastIndex));
            return sb.ToString();
        }

        /// <summary>
        /// Method to escape XML characters in an XML attribute
        /// </summary>
        /// <param name="input">Input string to process</param>
        /// <returns>Escaped string</returns>
        public static string EscapeXmlAttributeChars(string input)
        {
            input = EscapeXmlChars(input); // Sanitize string from illegal characters beside quotes
            input = input.Replace("\"", "&quot;");
            return input;
        }

        /// <summary>
        /// Method to generate an Excel internal password hash to protect workbooks or worksheets<br></br>This method is derived from the c++ implementation by Kohei Yoshida (<a href="http://kohei.us/2008/01/18/excel-sheet-protection-password-hash/">http://kohei.us/2008/01/18/excel-sheet-protection-password-hash/</a>)
        /// </summary>
        /// <remarks>WARNING! Do not use this method to encrypt 'real' passwords or data outside from PicoXLSX. This is only a minor security feature. Use a proper cryptography method instead.</remarks>
        /// <param name="password">Password string in UTF-8 to encrypt</param>
        /// <returns>16 bit hash as hex string</returns>
        public static string GeneratePasswordHash(string password)
        {
            if (string.IsNullOrEmpty(password)) { return string.Empty; }
            int passwordLength = password.Length;
            int passwordHash = 0;
            char character;
            for (int i = passwordLength; i > 0; i--)
            {
                character = password[i - 1];
                passwordHash = ((passwordHash >> 14) & 0x01) | ((passwordHash << 1) & 0x7fff);
                passwordHash ^= character;
            }
            passwordHash = ((passwordHash >> 14) & 0x01) | ((passwordHash << 1) & 0x7fff);
            passwordHash ^= (0x8000 | ('N' << 8) | 'K');
            passwordHash ^= passwordLength;
            return passwordHash.ToString("X");
        }

        /// <summary>
        /// Method to convert a date or date and time into the internal Excel time format (OAdate)
        /// </summary>
        /// <param name="date">Date to process</param>
        /// <param name="culture">CultureInfo for proper formatting of the decimal point</param>
        /// <returns>Date or date and time as Number</returns>
        /// <exception cref="FormatException">Throws a FormatException if the passed date cannot be translated to the OADate format</exception>
        /// <remarks>OA Date format starts at January 1st 1900 (actually 00.01.1900). Dates beyond this date cannot be handled by Excel under normal circumstances and will throw a FormatException</remarks>
        public static string GetOADateTimeString(DateTime date, CultureInfo culture)
        {
            try
            {
                double d = date.ToOADate();
                if (d < 0)
                {
                    throw new FormatException("The date is not in a valid range for Excel. Dates before 1900-01-01 are not allowed.");
                }
                return d.ToString("G", culture); //worksheet.DefaultRowHeight.ToString("G", culture) 
            }
            catch (Exception e)
            {
                throw new FormatException("ConversionException", "The date could not be transformed into Excel format (OADate).", e);
            }
        }

        #endregion

        #region subClasses

        /// <summary>
        /// Class to manage key value pairs (string / string). The entries are in the order how they were added
        /// </summary>
        public class SortedMap
        {
            private int count;
            private List<string> keyEntries;
            private List<string> valueEntries;
            private Dictionary<string, int> index;

            /// <summary>
            /// Number of map entries
            /// </summary>
            public int Count
            {
                get { return count; }
            }

            /// <summary>
            /// Gets the keys of the map as list
            /// </summary>
            public List<string> Keys
            {
                get { return keyEntries; }
            }

            /// <summary>
            /// Gets the values of the map as values
            /// </summary>
            public List<string> Values
            {
                get { return valueEntries; }
            }

            /// <summary>
            /// Default constructor
            /// </summary>
            public SortedMap()
            {
                keyEntries = new List<string>();
                valueEntries = new List<string>();
                index = new Dictionary<string, int>();
                count = 0;
            }


            /// <summary>
            /// Indexer to get the specific value by the key
            /// </summary>
            /// <param name="key">Key to corresponding value. Returns null if not found</param>
            public string this[string key]
            {
                get
                {
                    if (index.ContainsKey(key))
                    {
                        return valueEntries[index[key]];
                    }
                    return null;
                }
            }

            /// <summary>
            /// Adds a key value pair to the map. If the key already exists, only its index will be returned
            /// </summary>
            /// <param name="key">Key of the tuple</param>
            /// <param name="value">Value of the tuple</param>
            /// <returns>Position of the tuple in the map as index (zero-based)</returns>
            public int Add(string key, string value)
            {
                if (index.ContainsKey(key))
                {
                    return index[key];
                }
                else
                {
                    index.Add(key, count);
                    keyEntries.Add(key);
                    valueEntries.Add(value);
                    count++;
                    return count - 1;
                }
            }

            /// <summary>
            /// Gets whether the specified key exists in the map
            /// </summary>
            /// <param name="key">Key to check</param>
            /// <returns>True if the entry exists, otherwise false</returns>
            public bool ContainsKey(string key)
            {
                return index.ContainsKey(key);
            }

        }

        /// <summary>
        /// Class to manage XML document paths
        /// </summary>
        public class DocumentPath
        {
            /// <summary>
            /// File name of the document
            /// </summary>
            public string Filename { get; set; }
            /// <summary>
            /// Path of the document
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// Default constructor
            /// </summary>
            public DocumentPath()
            {
            }

            /// <summary>
            /// Constructor with defined file name and path
            /// </summary>
            /// <param name="filename">File name of the document</param>
            /// <param name="path">Path of the document</param>
            public DocumentPath(string filename, string path)
            {
                Filename = filename;
                Path = path;
            }

            /// <summary>
            /// Method to return the full path of the document
            /// </summary>
            /// <returns>Full path</returns>
            public string GetFullPath()
            {
                if (Path == null) { return Filename; }
                if (Path == "") { return Filename; }
                if (Path[Path.Length - 1] == System.IO.Path.AltDirectorySeparatorChar || Path[Path.Length - 1] == System.IO.Path.DirectorySeparatorChar)
                {
                    return System.IO.Path.AltDirectorySeparatorChar.ToString() + Path + Filename;
                }
                else
                {
                    return System.IO.Path.AltDirectorySeparatorChar.ToString() + Path + System.IO.Path.AltDirectorySeparatorChar.ToString() + Filename;
                }
            }

        }
        #endregion

    }
}
