'Miles33/TeraDP
'File where writing and debugging a sequential (batch) workflow
Option Explicit On
Option Strict On
Option Compare Binary
Option Infer On

Imports Microsoft.VisualBasic
Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Xml
Imports System.Xml.Linq
Imports System.Xml.XPath
Imports System.Xml.Xsl
Imports TeraDP.GN4.Server
Imports TeraDP.GN4.Server.CodeActivity
Imports System.IO
Imports BD_Crosswords




'<codeWorkflow
'  xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd">
Public Class SequentialWorkflow
    '--------------------- Sequential workflow sub/functions/fields go here
    '<Members>
    '  <![CDATA[
    'accepted file types
    Enum FileType
        unknown
        image
        audio
        video
        office
        pdf
        text
    End Enum

    Sub ProcessPDF(data As TeraDP.GN4.Workflow.IActivityData, xmp As XDocument, filesNumber As Integer, createImg As Boolean)
        If data Is Nothing Or xmp Is Nothing Then Return

        'extract the text from file: return a XML document as binary file
        Dim readAct As TransformData = New TransformData(Context) With {.Name = "readContentFromPdf"}
        readAct.Data = data
        Dim step1 As TeraDP.GN4.Common.Step = New TeraDP.GN4.Common.Step()
        step1.Conversion = "ReadText"

        'requested text format: xml, html, text or autodetect
        step1.Parameters.Add("text")
        readAct.Steps.Add(step1)
        Dim readRes As TransformDataResult = readAct.Do()

        'move the binary file to a XML document because TransformXml needs a XML
        Dim toXmlAct As LoadXml = New LoadXml(Context) With {.Name = "contentPdfToXml"}
        toXmlAct.Data = readRes.DataOut
        Dim toXmlRes As LoadXmlResult = toXmlAct.Do()

        'insert the textual content into the XMP file to import
        Dim completeXmpAct As TransformXml = New TransformXml(Context) _
            With {.Name = "completeOfficeXmp", .Description = "Preparing XMP info..."}
        completeXmpAct.XmlIn = xmp

        'read the textual content
        completeXmpAct.Pars.Add("richText", toXmlRes.XmlOut)
        'stylesheet that appends the textual content to the PDF metadata
        completeXmpAct.Xslt =
            <xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:gn4="urn:schemas-teradp-com:gn4tera"
                xmlns:lc="http://www.teradp.com/schemas/GN4/1/LoginContext.xsd"
                xmlns:fn="http://www.teradp.com/schemas/GN4/1/Xslt"
                xmlns:dc="http://purl.org/dc/elements/1.1/"
                xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                xmlns:xmp="http://ns.adobe.com/xap/1.0/"
                xmlns:xapGImg="http://ns.adobe.com/xap/1.0/g/img/"
                xmlns:xmpTPg="http://ns.adobe.com/xap/1.0/t/pg/"
                xmlns:stDim="http://ns.adobe.com/xap/1.0/sType/Dimensions#"
                xmlns:pdf="http://ns.adobe.com/pdf/1.3/">
                <xsl:param name="pars"/>
                <xsl:template match="@*|node()">
                    <xsl:copy>
                        <xsl:apply-templates select="@*|node()"/>
                        <xsl:if test="name(.)='rdf:RDF'">
                            <gn4:richText>
                                <xsl:copy-of select="$pars/*/add[@key='richText']/*/richTextPlain/node()"/>
                            </gn4:richText>
                        </xsl:if>
                    </xsl:copy>
                </xsl:template>
            </xsl:stylesheet>
        Dim completeXmpRes As TransformXmlResult = completeXmpAct.Do()
        'completeXmpRes.XmlOut
    End Sub

    Function GetFileTypeList(ByRef allowedString As String) As List(Of FileType)
        Dim allowedFileTypes As List(Of FileType) = New List(Of FileType)()
        Dim cleanString As String = String.Empty
        If Not String.IsNullOrEmpty(allowedString) Then
            Dim fTypeList As String() = allowedString.ToLower().Split({","c}, StringSplitOptions.RemoveEmptyEntries)
            If fTypeList IsNot Nothing Then
                For Each fType As String In fTypeList
                    Dim token As String = fType.Trim()
                    If Not String.IsNullOrEmpty(token) Then
                        If FileType.IsDefined(GetType(FileType), token) Then
                            allowedFileTypes.Add(CType([Enum].Parse(GetType(FileType), token), FileType))
                            cleanString &= token & ", "
                        Else
                            Utils.LogWarning(String.Format("The file type '{0}' is not valid", token))
                        End If
                    End If
                Next
                allowedString = cleanString.TrimEnd(New Char() {","c, " "c})
            End If
        End If
        Return allowedFileTypes
    End Function

    Class FileToProcess
        Public data As TeraDP.GN4.Workflow.IActivityData
        Public xmp As XDocument
    End Class

    Function CheckDuplicate(ArticleName As String, FolderPath As String) As Boolean
        Dim findDuplicate As Search = New Search(Context) With {.Name = "SearchArticle", .Description = "Checking article [" & ArticleName & "] already exists in [" & FolderPath & "]"}
        findDuplicate.Pars.Add("articleName", ArticleName)
        findDuplicate.Pars.Add("folderPath", FolderPath)
        findDuplicate.XQuery = "gn4:article[@name=$articleName and gn4:folderRef/nav:refObject/gn4:folder/@path=$folderPath]"
        Dim findDuplicateRes As SearchResult = findDuplicate.Do()
        If findDuplicateRes.IdsCount > 0 Then
            Return True
        Else
            Return False
        End If

    End Function

    Sub CreateArticle(tTextElement As XElement, ArticleName As String, overwrite As Boolean, FolderPath As String, imgIdList As IList(Of Integer), textFormat As String, typography As String, title As String)
        Dim counter As Int32
        counter = 0
        Dim tmpname As String = ArticleName

        If Not (overwrite) Then
            Do While CheckDuplicate(tmpname, FolderPath) = True
                counter = counter + 1
                tmpname = ArticleName + "~" + counter.ToString()
            Loop
        End If

        ArticleName = tmpname
        'prepare the complete article txts node, merging the body with photoCaption elements using the provided img id list
        Dim txtsNode As XElement = New XElement("txts")
        'in this example we assume the tTextElement is an xml already compatible with a txt.tText structure:
        ' <tText>
        ' <t:p xmlns:t="http://www.teradp.com/schemas/GN3/t.xsd" ><t:tag n="mytag"/>Some text</t:p>
        ' </tText>
        Dim bodyElement As XElement =
            <body name=<%= ArticleName %>>
                <folderRef>
                    <keyVal><%= FolderPath %>
                    </keyVal>
                </folderRef><%= tTextElement %>
            </body>
        Dim headElement As XElement =
            <head name=<%= ArticleName + ",hd" %>>
                <folderRef>
                    <keyVal><%= FolderPath %></keyVal>
                </folderRef>
                <tText>
                    <t:p xmlns:t="http://www.teradp.com/schemas/GN3/t.xsd">
                        <t:tag n="sav"/>
                        <t:tag n="colname"/>
                        <t:tag n="fill '.' 3pt,2"/>Chess<t:bell/><t:tag n="res"/><t:tag n="f 177"/><t:tag n="grey 100"/>Patrick Foley
                                              </t:p>
                </tText>
            </head>
        'txtsNode.Add(headElement)
        txtsNode.Add(bodyElement)
        Dim i As Integer = 0
        If imgIdList IsNot Nothing Then
            For Each imgId In imgIdList
                Dim txtName As String = ArticleName & "ph" & i.ToString()
                Dim imgIdString As String = "obj" & imgId
                Dim photoCaptionNode As XElement =
                    <photoCaption name=<%= txtName %>>
                        <folderRef>
                            <keyVal><%= FolderPath %></keyVal>
                        </folderRef>
                        <tText>
                            <t:p xmlns:t="http://www.teradp.com/schemas/GN3/t.xsd">
                                <t:tag n="rem" p0="1"/>Chess<t:tag n="rem" p0="0"/>
                            </t:p>
                        </tText>
                        <ref idref=<%= imgIdString %>/>
                    </photoCaption>
                txtsNode.Add(photoCaptionNode)
                i = i + 1
            Next
        End If
        Dim xArticle As XElement =
            <objects>
                <article name=<%= ArticleName %>>
                    <folderRef>
                        <keyVal><%= FolderPath %></keyVal>
                    </folderRef>
                    <authors>Back4</authors>
                    <title><%= ArticleName %></title>
                    <instructions></instructions>
                    <%= txtsNode %>
                </article>
                <txtGeometry name=<%= Guid.NewGuid().ToString() %>>
                    <txtRef objectType="body">
                        <keyRef objectType="folder">
                            <keyVal>
                                <%= FolderPath %>
                            </keyVal>
                        </keyRef>
                        <keyVal>
                            <%= ArticleName %>
                        </keyVal>
                    </txtRef>
                    <regionRef objectType="region">
                        <keyRef objectType="title">
                            <keyVal>
                                <%= title %>
                            </keyVal>
                        </keyRef>
                        <keyVal>Print</keyVal>
                    </regionRef>
                    <folderRef objectType="folder">
                        <keyVal>
                            <%= FolderPath %>
                        </keyVal>
                    </folderRef>
                    <contextRef objectType="justContext">
                        <keyRef objectType="justScope">
                            <keyVal>
                                <%= typography %>
                            </keyVal>
                        </keyRef>
                        <keyVal>
                            <%= textFormat %>
                        </keyVal>
                    </contextRef>
                    <data/>
                    <localGeometry/>
                    <jumps/>
                </txtGeometry>
            </objects>
        Dim importArticle As ImportXml = New ImportXml(Context) With {.Name = "Create article", .Description = "Creating article [" & ArticleName & "]"}
        importArticle.XmlIn = New XDocument(xArticle)
        Dim importArticleRes As ImportXmlResult = importArticle.Do()
    End Sub
    Function FindArticleXml(artName As String, folderPath As String) As XElement
        FindArticleXml = Nothing
        Dim findArticle As Search = New Search(Context) With {.Name = "find existing article"}
        findArticle.XQuery = "gn4:article[@name=$artName and gn4:folderRef/nav:refObject/gn4:folder[@path=$folderPath]]"
        findArticle.Pars.Add("artName", artName)
        findArticle.Pars.Add("folderPath", folderPath)
        Dim findArticleResult As SearchResult = findArticle.Do()
        'load the article and get its current text
        If findArticleResult.IdsCount > 0 Then
            Dim loadArticle As LoadObjects = New LoadObjects(Context) With {.Name = "load article"}
            loadArticle.ObjectIds = findArticleResult.IdsOut
            loadArticle.Xslt = <xsl:copy-of select="gn4:txts/gn4:body/gn4:tText" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"/>
            FindArticleXml = loadArticle.Do.XmlOut.Root
        End If
    End Function

    Function ProcessOffice(data As TeraDP.GN4.Workflow.IActivityData, xmp As XDocument, filesNumber As Integer) As String
        If data Is Nothing Or xmp Is Nothing Then Exit Function 'extract the text from file: return a XML document as binary file
        Dim readAct As TransformData = New TransformData(Context) With {.Name = "readContentFromOffice"}
        readAct.Data = data
        Dim step1 As TeraDP.GN4.Common.Step = New TeraDP.GN4.Common.Step()
        step1.Conversion = "ReadText"
        'requested text format: xml, html, text or autodetect
        step1.Parameters.Add("html")
        readAct.Steps.Add(step1)
        Dim readRes As TransformDataResult = readAct.Do()
        'move the binary file to a XML document because TransformXml needs a XML
        Dim toXmlAct As LoadXml = New LoadXml(Context) With {.Name = "contentOfficeToXml"}
        toXmlAct.Data = readRes.DataOut
        Dim toXmlRes As LoadXmlResult = toXmlAct.Do()
        Dim textData As String = toXmlRes.XmlOut.Descendants("root").FirstOrDefault.Value
        'If htmlData Is Nothing Then
        '    htmlData = toXmlRes.XmlOut.Root
        'End If
        'ImportOffice(xmp, New XDocument(htmlData), filesNumber)
        Return textData
    End Function

    '  ]]>
    '</Members>
    '--------------------- End of sequential workflows sub/functions/fields
    <STAThread()>
    Private Sub __Do()
        '----------------------- Sequential workflow code goes here
        '<Sequential>
        '  <![CDATA[

        '1. parse the file to get an xml of its metadata, including info about the mime type
        '2. based on the mime type, pass the data to a transformData activity (processoffice)
        '3. the transformdata returns an xml, or text read from th inout data
        '4. massage the output to create an xmldcoument
        '5. pass the xmldoc to an importxml activity to create the gn4 content
        Dim folderPath = Context.ParValue("folderPath")
        Dim typography = Context.ParValue("typography")
        If typography = "" Then
            Utils.LogError("Please define the 'typography' parameter")
            Return
        End If
        Dim title = Context.ParValue("title")
        If title = "" Then
            Utils.LogError("Please define the 'title' parameter")
            Return
        End If
        Dim textFormat = Context.ParValue("textFormat")
        If textFormat = "" Then
            Utils.LogError("Please define the 'textFormat' parameter")
            Return
        End If
        Dim imgFolderPath = Context.ParValue("imgFolderPath")
        If imgFolderPath = "" Then
            Utils.LogError("Please define the 'imgFolderPath' parameter")
            Return
        End If

        'process the file one by one
        Dim dataList As List(Of TeraDP.GN4.Workflow.IActivityData) = New List(Of TeraDP.GN4.Workflow.IActivityData)(Context.Data)
        Dim idx As Integer = 0
        While idx < dataList.Count
            Dim data As TeraDP.GN4.Workflow.IActivityData = dataList(idx)
            If Not data Is Nothing Then
                'get the file name
                Dim filename As String = data.Info.SrcName

                Try
                    'extract the file metadata
                    Dim parseAct As Parse = New Parse(Context) With {.Name = "parse file", .Description = "Parsing uploaded files"}
                    parseAct.Data = data
                    'parsing options: how to create thumbnails and xmp document
                    parseAct.Options.ThumbnailSize = 0
                    parseAct.Options.PreviewSize = 0
                    parseAct.Options.XmpRoot = False
                    parseAct.Options.UseAttributes = False
                    parseAct.Options.NoClipPreview = False
                    parseAct.Options.ForceRasterize = True

                    Dim parseRes As ParseResult = parseAct.Do()



                    Dim xmp As XDocument = parseRes.XmlOut
                    Dim myxmlOut As String = ""
                    Dim myxmlOutSol As String = ""
                    Dim puzzle As String = ""
                    Dim grid As String = ""

                    Dim Xml_Crossword As Xml_Transform = New Xml_Transform()
                    myxmlOut = Xml_Crossword.Process(data.LocalPath, False)
                    myxmlOutSol = Xml_Crossword.ProcessSolution(data.LocalPath)
                    puzzle = Xml_Crossword.ProcessPuzzle(data.LocalPath)
                    grid = Xml_Crossword.ProcessGrid(data.LocalPath).Trim()
                    Dim crossWordName As String = data.Info.SrcNameNoExtension

                    Dim firstChar As String = crossWordName(0)
                    Dim lastChar As String = crossWordName(crossWordName.Length - 1)
                    If IsNumeric(lastChar) Then
                        lastChar = ""
                    End If

                    Dim PreviousName As String = firstChar + (Convert.ToInt32(puzzle) + 1).ToString() + lastChar
                    Dim PreviousCrossWordsXml As XElement = FindArticleXml(PreviousName, folderPath)
                    If PreviousCrossWordsXml IsNot Nothing Then
                        Dim xDoc As XmlDocument = New XmlDocument()
                        Dim xDocSol As XmlDocument = New XmlDocument()
                        xDoc = PreviousCrossWordsXml.GetXmlDocument()
                        xDocSol.LoadXml(myxmlOutSol)
                        Dim gridnum As Integer = 0

                        For Each node As XmlNode In xDoc.DocumentElement("t:t")
                            If node.InnerText.Contains("GRID") Then
                                gridnum = Convert.ToInt32(node.InnerText.Split(CChar(":"))(1).Replace("CRYPTIC", "").Replace("QUICK", "").Trim())
                                Exit For
                            End If
                        Next

                        Dim PreviousGridNumber As Integer = Convert.ToInt32(gridnum)
                        Dim PreviousNum As String = grid.Trim()

                        If PreviousGridNumber < 10 Then
                            PreviousNum = "0" + PreviousGridNumber.ToString()
                        End If

                        Dim alreadyDone As Boolean = False
                        For Each node As XmlNode In xDoc.DocumentElement("t:t")
                            If node.InnerText.Contains("PREVIOUS SOLUTION") Then
                                alreadyDone = True
                                Exit For
                            End If
                        Next

                        If Not alreadyDone Then
                            For Each node As XmlNode In xDocSol.DocumentElement("t:t")
                                Dim importNode As XmlNode = xDoc.ImportNode(node, True)
                                xDoc.DocumentElement("t:t").AppendChild(importNode)
                            Next
                            Dim updated As String = xDoc.OuterXml

                            Dim articleImgIdsUpdate As New List(Of Integer)
                            'Find the previous puzzle's solution image
                            Dim findImgUpdate As Search = New Search(Context) With {.Name = "check img exists"}
                            findImgUpdate.XQuery = "gn4:img[@name=$imgName and gn4:folderRef/nav:refObject/gn4:folder[@path=$imgFolderPath]]"
                            findImgUpdate.Pars.Add("imgName", PreviousNum & "grid")
                            findImgUpdate.Pars.Add("imgFolderPath", imgFolderPath)
                            Dim findImgResultUpdate As SearchResult = findImgUpdate.Do()

                            'If we find a previous solution add it to this article
                            If findImgResultUpdate.IdsCount > 0 Then
                                articleImgIdsUpdate.Add(findImgResultUpdate.IdsOut(0))
                            End If

                            CreateArticle(xDoc.GetXElement(), PreviousName, True, folderPath, articleImgIdsUpdate, textFormat, typography, title)
                        End If
                    End If

                    Dim GridNumber As Integer = Convert.ToInt32(grid)
                    Dim Num As String = grid.Trim()

                    If GridNumber < 10 Then
                        Num = "0" + GridNumber.ToString()
                    End If

                    Dim articleImgIds As New List(Of Integer)
                    'Find the previous puzzle's solution image
                    Dim findImg As Search = New Search(Context) With {.Name = "check img exists"}
                    findImg.XQuery = "gn4:img[@name=$imgName and gn4:folderRef/nav:refObject/gn4:folder[@path=$imgFolderPath]]"
                    findImg.Pars.Add("imgName", Num & "grid")
                    findImg.Pars.Add("imgFolderPath", imgFolderPath)
                    Dim findImgResult As SearchResult = findImg.Do()

                    'If we find a previous solution add it to this article
                    If findImgResult.IdsCount > 0 Then
                        articleImgIds.Add(findImgResult.IdsOut(0))
                    End If

                    Dim xmlSourceTransformed As XElement = XElement.Parse(myxmlOut)
                    Dim crossWordXml As XElement = xmlSourceTransformed.XPathSelectElement("/")

                    Dim currrentCrossWordsXml As XElement = FindArticleXml(crossWordName, folderPath)
                    If (currrentCrossWordsXml Is Nothing) Then
                        CreateArticle(crossWordXml, crossWordName, True, folderPath, articleImgIds, textFormat, typography, title)
                    End If

                Catch e As Exception
                    Utils.LogError("Error processing file " + data.Info.SrcName + ": " + e.InnerException.StackTrace + e.InnerException.Message)
                End Try

            End If
            idx += 1
        End While


        '  ]]>
        '</Sequential>
        '----------------------- End of sequential workflow code
    End Sub
End Class
'</codeWorkflow>