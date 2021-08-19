'Miles33/Tera DP
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
Imports Sowetan_Crossword


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
        Dim findDuplicate As Search = New Search(Context) With {.Name = "SearchArticle", .Description = "Checking article [" & amp; ArticleName &amp; "]  already exists in [" &amp; FolderPath &amp; "]"}
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


    Sub CreateArticle(tTextElement As XElement, articleName As String, overwrite As Boolean, folderPath As String, imgIdList As IList(Of Integer), textFormat As String, typography As String, title As String)
        Dim counter As Int32
        counter = 0
        Dim tmpname As String = articleName
        If Not (overwrite) Then
            Do While CheckDuplicate(tmpname, folderPath) = True
                counter = counter + 1
                tmpname = articleName + "~" + counter.ToString()
            Loop
        End If
        articleName = tmpname
        'prepare the complete article txts node, merging the body with photoCaption elements using the provided img id list
        Dim txtsNode As XElement = New XElement("txts")
        'in this example we assume the tTextElement is an xml already compatible with a txt.tText structure:
        '  < tText>
        '       <t:p xmlns: t = "http://www.teradp.com/schemas/GN3/t.xsd" ><t:tag n="mytag"/>Some text</t:p>
        '  </tText>

        Dim bodyElement As XElement = <body name=<%= articleName %>>
                                          <folderRef>
                                              <keyVal><%= folderPath %></keyVal>
                                          </folderRef>
                                          <%= tTextElement %>
                                      </body>

        Dim headElement As XElement = <head name=<%= articleName + ",hd" %>>
                                          <folderRef>
                                              <keyVal><%= folderPath %></keyVal>
                                          </folderRef>
                                          <tText>
                                              <t:p xmlns:t="http://www.teradp.com/schemas/GN3/t.xsd">
                                                  <t:tag n="sav"/>
                                                  <t:tag n="colname"/>
                                                  <t:tag n="fill '.' 3pt,2"/> Chess <t:bell/><t:tag n="res"/><t:tag n="f 177"/><t:tag n="grey 100"/> Patrick Foley
                                              </t:p>
                                          </tText>
                                      </head>

        'txtsNode.Add(headElement)
        txtsNode.Add(bodyElement)
        Dim i As Integer = 0
        If imgIdList IsNot Nothing Then
            For Each imgId In imgIdList
                Dim txtName As String = articleName & "ph" & i.ToString()
                Dim imgIdString As String = "obj" & imgId
                Dim photoCaptionNode As XElement = <photoCaption name=<%= txtName %>>
                                                       <folderRef>
                                                           <keyVal><%= folderPath %></keyVal>
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

        Dim xArticle As XElement = <objects><article name=<%= articleName %>>
                                       <folderRef>
                                           <keyVal><%= folderPath %></keyVal>
                                       </folderRef>
                                       <authors>Back4</authors>
                                       <title><%= articleName %></title>
                                       <instructions></instructions>
                                       <%= txtsNode %>
                                       </article>
                                       <txtGeometry name=<%= Guid.NewGuid().ToString() %>>
                                           <txtRef objectType="body">
                                               <keyRef objectType="folder">
                                                   <keyVal><%= folderPath %></keyVal>
                                               </keyRef>
                                               <keyVal><%= articleName %></keyVal>
                                           </txtRef>
                                           <regionRef objectType="region">
                                               <keyRef objectType="title">
                                                   <keyVal><%= title %></keyVal>
                                               </keyRef>
                                               <keyVal>Print</keyVal>
                                           </regionRef>
                                           <folderRef objectType="folder">
                                               <keyVal><%= folderPath %></keyVal>
                                           </folderRef>
                                           <contextRef objectType="justContext">
                                               <keyRef objectType="justScope">
                                                   <keyVal><%= typography %></keyVal>
                                               </keyRef>
                                               <keyVal><%= textFormat %></keyVal>
                                           </contextRef>
                                           <data/>
                                           <localGeometry/>
                                           <jumps/>
                                       </txtGeometry>
                                   </objects>

        Dim importArticle As ImportXml = New ImportXml(Context) With {.Name = "Create article", .Description = "Creating article [" & amp; articleName &amp; "]"}
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
            loadArticle.RefKeys = True
            loadArticle.ObjectIds = findArticleResult.IdsOut
            loadArticle.Xslt = <article id="{@id}" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                                   <images>
                                       <xsl:for-each select="gn4:txts/gn4:photoCaption">
                                           <image id="{gn4:ref/nav:refObject/gn4:img/@id}">
                                               <xsl:copy-of select="gn4:ref/nav:refObject/gn4:img/gn4:low"/>
                                               <ref objectType="img">
                                                   <keyRef objectType="folder">...</keyRef>
                                                   <keyVal>puzzle_28012020</keyVal>
                                               </ref>
                                           </image>
                                       </xsl:for-each>
                                   </images>

                               </article>
            FindArticleXml = loadArticle.Do.XmlOut.Root
        End If
    End Function

    Function ProcessOffice(data As TeraDP.GN4.Workflow.IActivityData, xmp As XDocument, filesNumber As Integer) As String
        If data Is Nothing Or xmp Is Nothing Then Return ""

        'extract the text from file: return a XML document as binary file
        Dim readAct As TransformData = New TransformData(Context) With {.Name = "readContentFromOffice"}
        readAct.Data = data

        Dim step1 As TeraDP.GN4.Common.Step = New TeraDP.GN4.Common.Step()
        step1.Conversion = "ReadText"
        'requested text format: xml, html, text or autodetect
        step1.Parameters.Add("text")

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
        '  ImportOffice(xmp, New XDocument(htmlData), filesNumber)
        Return textData
    End Function


    Function PreviousWorkDay(ByVal curDate As DateTime) As String
        Do
            curDate = curDate.AddDays(-1)
        Loop While IsWeekend(curDate)
        Return curDate.ToString("ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture)
    End Function

    Function IsWeekend(ByVal myDate As DateTime) As Boolean
        Return myDate.DayOfWeek = DayOfWeek.Saturday OrElse myDate.DayOfWeek = DayOfWeek.Sunday
    End Function


    '  ]]>
    '</Members>
    '--------------------- End of sequential workflows sub/functions/fields
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
        Dim imgFolderPath = Context.ParValue("imgFolderPath")
        Dim sourceKind = Context.ParValue("sourceKind")
        'sourceKind must be in the list:
        ' 10MinuteCrossword
        ' 20MinuteCrossword
        ' Bridge
        ' Chess
        ' Sudoku
        ' TwoSpeedCrossword


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
                    parseAct.Options.ThumbnailSize = 120
                    parseAct.Options.ThumbnailQuality = 80
                    parseAct.Options.ImageMinRes = 72
                    parseAct.Options.DefaultRes = 72
                    parseAct.Options.PreviewSize = 0
                    parseAct.Options.XmpRoot = False
                    parseAct.Options.UseAttributes = False
                    parseAct.Options.NoClipPreview = False
                    parseAct.Options.ForceRasterize = True

                    Dim parseRes As ParseResult = parseAct.Do()

                    Dim xmp As XDocument = parseRes.XmlOut
                    If Not parseRes.MimeOut Is Nothing Then
                        'check the mime type of the uploaded file
                        Dim mime As String = parseRes.MimeOut
                        Dim mytext As String = ProcessOffice(data, xmp, dataList.Count)
                        Dim myxmlOut As String = ""
                        Dim xmlFile As String = data.LocalPath.ToString()

                        Dim xmlDoc As New XmlDocument()
                        xmlDoc.Load(xmlFile)

                        Dim xml_30_Min_Transform As Herald_Crossword_Transform = New Herald_Crossword_Transform()
                        myxmlOut = xml_30_Min_Transform.Process(xmlDoc.InnerXml)

                        Dim xmlSourceTransformed As XElement = XElement.Parse(myxmlOut)
                        'xmlSourceTransformed.Save("c:\temp\" &amp;amp; data.Info.SrcName)
                        Dim crossWordXml As XElement = xmlSourceTransformed.XPathSelectElement("//crossword")
                        Dim crossWordName As String = crossWordXml.Attribute("id").Value()
                        Dim crossWordTText As XElement = crossWordXml.XPathSelectElement("//crossword/article/tText")
                        Dim solutionXml As XElement = xmlSourceTransformed.XPathSelectElement("//solution")
                        Dim solutionName As String = (CInt(crossWordName) + 2).ToString()
                        Dim solutionTText As XElement = xmlSourceTransformed.XPathSelectElement("//solution/article/tText")
                        Dim imgName As String = crossWordXml.<article>.@grid

                        '1 check img exists and return its id, otherwise 0


                        'load the article and get its current text, if found.
                        'if not existing, the node will be empty
                        Dim currrentCrossWordsXml As XElement = FindArticleXml(crossWordName, folderPath)

                        If currrentCrossWordsXml IsNot Nothing Then
                            If currrentCrossWordsXml.Value.Trim.StartsWith("QUICK") Or currrentCrossWordsXml.Value.Trim.StartsWith("CRYPTIC") Then
                                Dim mergedXmlText As XElement = <tText/>
                                mergedXmlText.Add(crossWordXml.DescendantNodes())
                                mergedXmlText.Add(currrentCrossWordsXml.DescendantNodes)
                                CreateArticle(mergedXmlText, crossWordName, True, folderPath, Nothing)
                            Else
                                CreateArticle(crossWordTText, crossWordName, True, folderPath, Nothing)
                            End If
                        Else
                            CreateArticle(crossWordTText, crossWordName, True, folderPath, Nothing)
                        End If

                        Dim currrentSolutionXml As XElement = FindArticleXml(solutionName, folderPath)

                        If currrentSolutionXml IsNot Nothing Then
                            'find the images already linked to the article, in order not to remove them when updating its structure
                            Dim findArticleImg As Search = New Search(Context) With {.Name = "check img exists in existing article"}
                            findArticleImg.XQuery = "gn4:img[nav:referencing/nav:referencingBy[@attrName='photoCaption.ref']/nav:refs/nav:refObject/gn4:*[starts-with(@name,$solutionName)]/gn4:folderRef/nav:refObject/gn4:folder[@path=$folderPath] and gn4:folderRef/nav:refObject/gn4:folder[@path=$imgFolderPath]]"
                            findArticleImg.Pars.Add("solutionName", solutionName)
                            findArticleImg.Pars.Add("folderPath", folderPath)
                            findArticleImg.Pars.Add("imgName", imgName)
                            findArticleImg.Pars.Add("imgFolderPath", imgFolderPath)
                            Dim findArticleImgResult As SearchResult = findArticleImg.Do()
                            'if the solution name exists and starts with 'repvious solution' we have to overwrite
                            If currrentCrossWordsXml.Value.Trim.StartsWith("QUICK") Or currrentCrossWordsXml.Value.Trim.StartsWith("CRYPTIC") Then
                                CreateArticle(solutionTText, solutionName, True, folderPath, findArticleImgResult.IdsOut)
                            Else
                                'if it does not start with that string, it contains the definitions, and we append the solutions
                                Dim mergedXmlText As XElement = <tText/>
                                mergedXmlText.Add(currrentSolutionXml.DescendantNodes())
                                mergedXmlText.Add(solutionTText.DescendantNodes)
                                CreateArticle(mergedXmlText, solutionName, True, folderPath, findArticleImgResult.IdsOut)
                            End If
                        Else
                            CreateArticle(solutionTText, solutionName, True, folderPath, Nothing)
                        End If



                    End If
                Catch e As Exception
                    Console.WriteLine(e.Message)
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
