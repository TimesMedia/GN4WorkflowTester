'Miles33/Tera DP
Imports System.Collections.Specialized
Imports System.IO
Imports System.Text
Imports System.Windows.Controls.Primitives
Imports System.Xml
Imports System.Xml.Serialization
Imports TeraDP.GN4.Common
Imports TeraDP.GN4.Server

Public Class WorkflowFileHelper

  Private SourceFolderPath As String

  Shared Property IsSequential As Nullable(Of Boolean) = Nothing
  Shared ReadOnly xWFResNS As XNamespace = "http://www.teradp.com/schemas/GN4/1/WFRes.xsd"
  Public Shared MainWnd As MainWindow = Nothing

  Public Sub New(sourcePath As String)
    SourceFolderPath = sourcePath
  End Sub

  Public Function ComposeWorkflowString(isSequential As Boolean, wfCommentHeader As String, wfComments As String,
    referencesList As List(Of String), description As String, displayProgress As Nullable(Of Boolean), timeout As TimeSpan) As String
    Try
      Dim wfString As StringBuilder = New StringBuilder()
      Dim xWorkflow As XDocument = ComposeWorkflowDocument(isSequential, wfCommentHeader, wfComments, referencesList,
        description, displayProgress, timeout)

      If xWorkflow IsNot Nothing Then
        Dim xmlSettings As New XmlWriterSettings()
        xmlSettings.OmitXmlDeclaration = True
        xmlSettings.Indent = True
        Using writer As XmlWriter = XmlWriter.Create(wfString, xmlSettings)
          xWorkflow.Save(writer)
        End Using
      End If
      Return wfString.ToString()
    Catch ex As Exception
      Return String.Format("Error parsing the source code: {0}", ex.Message)
    End Try
  End Function

  Private Function ComposeWorkflowDocument(isSequential As Boolean, wfCommentHeader As String, wfComments As String,
    referencesList As List(Of String), description As String, displayProgress As Nullable(Of Boolean), timeout As TimeSpan) As XDocument
    WorkflowFileHelper.IsSequential = isSequential

    Dim result As XDocument = New XDocument()

    If wfCommentHeader IsNot Nothing AndAlso Not String.IsNullOrEmpty(wfCommentHeader.Trim()) Then
      Dim xCommentHeader As XComment = New XComment(" " & wfCommentHeader.Trim() & " ")
      result.Add(xCommentHeader)
    End If

    If wfComments IsNot Nothing AndAlso Not String.IsNullOrEmpty(wfComments.Trim()) Then
      Dim xComments As XComment = New XComment(" " & wfComments.Trim() & vbNewLine)
      result.Add(xComments)
    End If

    Dim xRoot As XElement = <codeWorkflow xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd"></codeWorkflow>
    result.Add(xRoot)
    If String.IsNullOrEmpty(SourceFolderPath) Then
      Return result
    End If

    Dim wfToken As String = "Sequential"
    If Not isSequential Then
      wfToken = "Navigator"
    End If

    Dim importsList As List(Of String) = Nothing
    Dim membersCode As String = Nothing
    Dim workflowCode As String = Nothing

    Dim sourceFilePath As String = String.Format("{0}\{1}Program.vb", SourceFolderPath.TrimEnd({"/"c, "\"c, " "c}), wfToken)
    If File.Exists(sourceFilePath) Then
      Dim sourceCode As String = File.ReadAllText(sourceFilePath)
      Dim members As String = Nothing
      ParseSourceFile(sourceCode, wfToken, importsList, membersCode, workflowCode)

      If referencesList IsNot Nothing AndAlso referencesList.Count > 0 Then
        Dim xReferences As XElement = <References xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd"/>
        For Each referenceItem As String In referencesList
          Dim xReference As XElement = <Reference xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd"><%= referenceItem %></Reference>
          xReferences.Add(xReference)
        Next
        xRoot.Add(xReferences)
      End If

      If importsList IsNot Nothing AndAlso importsList.Count > 0 Then
        Dim xImports As XElement = <Imports xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd"/>
        For Each importAssembly As String In importsList
          Dim xImport As XElement = <Import xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd"><%= importAssembly %></Import>
          xImports.Add(xImport)
        Next
        xRoot.Add(xImports)
      End If

      If Not String.IsNullOrEmpty(membersCode) Then
        Dim xMembersCData As XCData = New XCData(membersCode)
        Dim xMembers As XElement = <Members xmlns="http://www.teradp.com/schemas/GN4/1/WFRes.xsd"/>
        xMembers.Add(xMembersCData)
        xRoot.Add(xMembers)
      End If

      If Not String.IsNullOrEmpty(workflowCode) Then
        Dim xWorkflowCData As XCData = New XCData(workflowCode)
        Dim xWorkflow As XElement = New XElement(WorkflowFileHelper.xWFResNS + wfToken)
        If Not String.IsNullOrEmpty(description) Then
          xWorkflow.Add(New XAttribute("Description", description))
        End If
        If displayProgress IsNot Nothing Then
          If displayProgress = True Then
            xWorkflow.Add(New XAttribute("DisplayProgress", "true")) 'must be lowercase
          Else
            xWorkflow.Add(New XAttribute("DisplayProgress", "false"))
          End If
        End If
        If (Not isSequential) AndAlso timeout.TotalSeconds > 0 Then
          xWorkflow.Add(New XAttribute("TimeOut", timeout.ToString())) 'only for Navigator
        End If
        xWorkflow.Add(xWorkflowCData)
        xRoot.Add(xWorkflow)
      End If
    End If

    'indent and remove duplicate namespaces
    Dim xFinalWorkflow As XDocument = Nothing
    Using ms As MemoryStream = New MemoryStream()
      result.Save(ms, SaveOptions.OmitDuplicateNamespaces)
      ms.Seek(0, SeekOrigin.Begin)

      Dim xmlSettings As New XmlWriterSettings()
      xmlSettings.OmitXmlDeclaration = True
      xmlSettings.Indent = True
      Using writer As XmlWriter = XmlWriter.Create(ms, xmlSettings)
        xFinalWorkflow = XDocument.Load(ms)
      End Using
    End Using

    Return xFinalWorkflow
  End Function

  Private Sub ParseSourceFile(sourceCode As String, wfToken As String, ByRef importsList As List(Of String), ByRef membersCode As String, ByRef workflowCode As String)
    If String.IsNullOrEmpty(sourceCode) Then
      Return 'no members code
    End If

    Dim insideMembers As Boolean = False
    Dim insideWorkflow As Boolean = False

    importsList = New List(Of String)
    Dim members As StringBuilder = New StringBuilder()
    Dim workflow As StringBuilder = New StringBuilder()

    Dim reader As StringReader = New StringReader(sourceCode)
    Do While reader.Peek <> -1
      Dim line As String = Nothing
      Dim readLine As String = reader.ReadLine()
      If readLine IsNot Nothing Then
        line = readLine.TrimStart(" "c)
      End If
      If Not String.IsNullOrEmpty(line) Then
        If line.StartsWith("'") Then
          'commented line
          Dim commentLine As String = line.TrimStart({"'"c, " "c})
          If commentLine.StartsWith("<Members", StringComparison.InvariantCultureIgnoreCase) Then
            insideMembers = True
            insideWorkflow = False
            Continue Do
          ElseIf commentLine.StartsWith("</Members", StringComparison.InvariantCultureIgnoreCase) Then
            insideMembers = False
            insideWorkflow = False
            Continue Do
          ElseIf commentLine.StartsWith(String.Format("<{0}", wfToken), StringComparison.InvariantCultureIgnoreCase) Then
            insideMembers = False
            insideWorkflow = True
            Continue Do
          ElseIf commentLine.StartsWith(String.Format("</{0}", wfToken), StringComparison.InvariantCultureIgnoreCase) Then
            insideMembers = False
            insideWorkflow = False
            Continue Do
          ElseIf commentLine.StartsWith("<![CDATA[", StringComparison.InvariantCultureIgnoreCase) Then
            Continue Do
          ElseIf commentLine.StartsWith("]]>", StringComparison.InvariantCultureIgnoreCase) Then
            Continue Do
          End If
        Else
          'not a comment
          If Not insideMembers And Not insideWorkflow Then
            If line.StartsWith("Imports", StringComparison.InvariantCultureIgnoreCase) Then
              'Imports
              insideMembers = False
              insideWorkflow = False
              Dim tokens() As String = line.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
              If tokens.Length > 1 AndAlso Not IsStandardImport(tokens(1)) Then
                importsList.Add(tokens(1))
              End If
            End If
          End If
        End If
      End If

      If insideMembers Then
        members.AppendLine(readLine)
      ElseIf insideWorkflow Then
        workflow.AppendLine(readLine)
      End If
    Loop

    Dim code As String = members.ToString().Trim()
    If Not String.IsNullOrEmpty(code) Then
      membersCode = System.Environment.NewLine + code + System.Environment.NewLine
    End If
    code = workflow.ToString().Trim()
    If Not String.IsNullOrEmpty(code) Then
      workflowCode = System.Environment.NewLine + code + System.Environment.NewLine
    End If
  End Sub

  Public Function ComposeSourceFile(ByRef isSequential As Boolean, importsList As List(Of String), membersCode As String, workflowCode As String) As String
    'read the file template
    Dim wfToken As String = "Sequential"
    If Not isSequential Then
      wfToken = "Navigator"
    End If
    Dim templateFilePath As String = String.Format("{0}\Resources\{1}Template.txt", SourceFolderPath.TrimEnd({"/"c, "\"c, " "c}), wfToken)
    If Not File.Exists(templateFilePath) Then
      Throw New Exception(String.Format("Cannot find file '{0}'.", templateFilePath))
    End If
    Dim templateString As String = File.ReadAllText(templateFilePath)

    Dim importsData As StringBuilder = New StringBuilder()
    If importsList IsNot Nothing AndAlso importsList.Count > 0 Then
      For Each importNamespace As String In importsList
        If Not WorkflowFileHelper.IsStandardImport(importNamespace) Then
          importsData.AppendLine(String.Format("Imports {0}", importNamespace))
        End If
      Next
    End If

    templateString = templateString.Replace("[imports placeholder]", importsData.ToString())
    templateString = templateString.Replace("[members placeholder]", membersCode)
    templateString = templateString.Replace("[code placeholder]", workflowCode)
    Return templateString
  End Function

  Public Sub ParseWorkflowDocument(
     xmlFile As XDocument,
     ByRef isSequential As Boolean,
     ByRef commentsList As List(Of String),
     ByRef referencesList As List(Of String),
     ByRef importsList As List(Of String),
     ByRef membersCode As String,
     ByRef workflowCode As String,
     ByRef description As String,
     ByRef displayProgress As Nullable(Of Boolean),
     ByRef timeout As TimeSpan
  )
    If xmlFile Is Nothing Then
      Throw New ArgumentNullException("xmlFile")
    End If

    'read comments
    For Each xComment As XNode In xmlFile.Nodes()
      If TypeOf (xComment) Is XComment Then
        If commentsList Is Nothing Then
          commentsList = New List(Of String)()
        End If
        commentsList.Add(CType(xComment, XComment).Value)
      End If
    Next

    Dim xRoot As XElement = xmlFile.Root
    If xRoot Is Nothing OrElse String.Compare(xRoot.Name.LocalName, "codeworkflow", True) <> 0 Then
      Throw New ArgumentNullException("Cannot find the 'codeworkflow' tag")
    End If

    'find the main workflow (<Sequential> or <Navigator>) code
    Dim xWorkflow As XElement = xRoot.Elements(WorkflowFileHelper.xWFResNS + "Sequential").FirstOrDefault
    If xWorkflow Is Nothing Then
      xWorkflow = xRoot.Element(WorkflowFileHelper.xWFResNS + "Navigator")
      If xWorkflow IsNot Nothing Then
        isSequential = False
      Else
        Throw New ArgumentNullException("Cannot find neither 'Sequential' or 'Navigator' tag")
      End If
    Else
      isSequential = True
    End If
    WorkflowFileHelper.IsSequential = isSequential

    'find workflow attributes
    description = xWorkflow.@Description
    Dim timeoutStr As String = xWorkflow.@TimeOut
    If Not String.IsNullOrEmpty(timeoutStr) Then
      Try
        Dim tmo As TimeSpan = TimeSpan.Parse(timeoutStr)
        If tmo.TotalSeconds > 0 Then
          timeout = tmo
        End If
      Catch
        'continue with no exception
      End Try
    End If
    displayProgress = String.Compare(xWorkflow.@DisplayProgress, "true", True) = 0

    If Not TypeOf (xWorkflow.FirstNode) Is XCData Then
      Throw New ArgumentNullException("Cannot find the main workflow code")
    End If
    workflowCode = CType(xWorkflow.FirstNode, XCData).Value

    'find the <Members> code
    Dim xMembers As XElement = xRoot.Elements(WorkflowFileHelper.xWFResNS + "Members").FirstOrDefault
    If xMembers IsNot Nothing AndAlso xMembers.FirstNode IsNot Nothing AndAlso TypeOf (xWorkflow.FirstNode) Is XCData Then
      membersCode = CType(xMembers.FirstNode, XCData).Value
    End If

    'find the <References> code
    Dim xReferences As XElement = xRoot.Element(WorkflowFileHelper.xWFResNS + "References")
    If xReferences IsNot Nothing Then
      For Each xReference As XElement In xReferences.Elements(WorkflowFileHelper.xWFResNS + "Reference")
        If referencesList Is Nothing Then
          referencesList = New List(Of String)()
        End If
        referencesList.Add(xReference.Value)
      Next
    End If

    'find the <Imports> code
    Dim xImports As XElement = xRoot.Element(WorkflowFileHelper.xWFResNS + "Imports")
    If xImports IsNot Nothing Then
      For Each xImport As XElement In xImports.Elements(WorkflowFileHelper.xWFResNS + "Import")
        If importsList Is Nothing Then
          importsList = New List(Of String)()
        End If
        importsList.Add(xImport.Value)
      Next
    End If
  End Sub

  Public Shared Function IsStandardImport(str As String)
    If String.IsNullOrEmpty(str) Then
      Return False
    End If

    Return String.Compare(str, "Microsoft.VisualBasic", True) = 0 _
      OrElse String.Compare(str, "System", True) = 0 _
      OrElse String.Compare(str, "System.Collections", True) = 0 _
      OrElse String.Compare(str, "System.Collections.Generic", True) = 0 _
      OrElse String.Compare(str, "System.Diagnostics", True) = 0 _
      OrElse String.Compare(str, "System.Linq", True) = 0 _
      OrElse String.Compare(str, "System.Threading.Tasks", True) = 0 _
      OrElse String.Compare(str, "System.Xml", True) = 0 _
      OrElse String.Compare(str, "System.Xml.Linq", True) = 0 _
      OrElse String.Compare(str, "System.Xml.XPath", True) = 0 _
      OrElse String.Compare(str, "System.Xml.Xsl", True) = 0 _
      OrElse String.Compare(str, "TeraDP.GN4.Server", True) = 0 _
      OrElse String.Compare(str, "TeraDP.GN4.Server.CodeActivity", True) = 0
  End Function

  Public Shared Function IsStandardReference(str As String)
    If String.IsNullOrEmpty(str) Then
      Return False
    End If

    Return str.EndsWith("GNClient.dll") _
      OrElse str.EndsWith("Common.dll") _
      OrElse str.EndsWith("Client.dll") _
      OrElse str.EndsWith("GNQuery.dll") _
      OrElse str.EndsWith("GNCx.dll") _
      OrElse str.EndsWith("WinUI.dll") _
      OrElse str.EndsWith("WebUI.dll") _
      OrElse str.EndsWith("Server.dll")
  End Function

  Public Sub SaveFile(fileName As String, text As String)
    fileName = WorkflowFileHelper.GetWFFileName(fileName)

    Dim dlg As Microsoft.Win32.SaveFileDialog = New Microsoft.Win32.SaveFileDialog()
    dlg.FileName = fileName
    dlg.Filter = "GN4 Workflows (wf_*.xml)|wf_*.xml|All files (*.*)|*.*"
    dlg.FilterIndex = 1
    Dim result As Nullable(Of Boolean) = dlg.ShowDialog()
    If Not result Then
      Return
    End If
    If dlg.FileNames IsNot Nothing AndAlso dlg.FileNames.Length > 0 Then
      File.WriteAllText(dlg.FileNames(0), text)
    End If
  End Sub

  Public Function LoadFile(ByRef fileName As String) As XDocument
    Dim dlg As Microsoft.Win32.OpenFileDialog = New Microsoft.Win32.OpenFileDialog()
    dlg.Multiselect = False
    dlg.Filter = "GN4 Workflows (wf_*.xml)|wf_*.xml|All files (*.*)|*.*"
    dlg.FilterIndex = 1
    Dim result As Nullable(Of Boolean) = dlg.ShowDialog()
    If Not result Then
      Return Nothing 'no file loaded
    End If
    If dlg.FileNames IsNot Nothing AndAlso dlg.FileNames.Length > 0 Then
      'generate the new .vb source file
      fileName = WorkflowFileHelper.GetCleanFileName(Path.GetFileNameWithoutExtension(dlg.FileNames(0)))
      Return XDocument.Load(dlg.FileNames(0))
    End If
    Return Nothing
  End Function

  Public Function FindSourceFile(isSequential As Boolean) As String
    Dim sourceFolder As String = Nothing
    If Not String.IsNullOrEmpty(SourceFolderPath) Then
      sourceFolder = SourceFolderPath.TrimEnd({"/"c, "\"c, " "c})
    End If

    Dim sourceFile As String = Nothing
    If isSequential Then
      sourceFile = String.Format("{0}\SequentialProgram.vb", sourceFolder)
    Else
      sourceFile = String.Format("{0}\NavigatorProgram.vb", sourceFolder)
    End If
    If String.IsNullOrEmpty(sourceFile) OrElse Not File.Exists(sourceFile) Then
      Throw New Exception(String.Format("Cannot find the '{0}' file.", sourceFile))
    End If
    Return sourceFile
  End Function

  Shared Function FindSourceFilesFolder() As String
    Try
      Dim executablePath As FileInfo = New FileInfo(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
      Dim rootPath As String = executablePath.Directory.Parent.FullName
      If Not String.IsNullOrEmpty(rootPath) Then
        Dim foundFiles() As String = Directory.GetFiles(rootPath, "SequentialProgram.vb", SearchOption.AllDirectories)
        If foundFiles.Count > 0 Then
          Dim sourceFile As FileInfo = New FileInfo(foundFiles(0))
          Return sourceFile.DirectoryName
        End If
      End If
    Catch ex As Exception
      'continue with no error
    End Try
    Return Nothing
  End Function

  Shared Function GetCleanFileName(wfFileName As String) As String
    If String.IsNullOrEmpty(wfFileName) Then
      wfFileName = "test-" & GetUniqueToken()
    End If
    wfFileName = wfFileName.Replace(" ", "")
    If wfFileName.StartsWith("wf_") Then
      wfFileName = wfFileName.Substring(3)
    End If
    If wfFileName.EndsWith(".xml") Then
      wfFileName = wfFileName.Substring(0, wfFileName.Length - 4)
    End If
    Return wfFileName
  End Function

  Shared Function GetWFFileName(wfFileName As String) As String
    wfFileName = GetCleanFileName(wfFileName)
    Return String.Format("wf_{0}.xml", wfFileName)
  End Function

  Shared Function FindBackupFilesFolder() As String
    Try
      Dim executablePath As FileInfo = New FileInfo(AppDomain.CurrentDomain.SetupInformation.ApplicationBase)
      Dim backupPath As String = String.Format("{0}\{1}",
        executablePath.Directory.ToString().TrimEnd({"/"c, "\"c, " "c}), "workflowTesterBackup")
      If Not String.IsNullOrEmpty(backupPath) Then
        'create the backup folder if missing 
        Directory.CreateDirectory(backupPath)
      End If
      Return backupPath
    Catch ex As Exception
      'continue with no error
    End Try
    Return Nothing
  End Function

  Shared Function GetUniqueToken() As String
    Return String.Format("{0:yyyyMMddHHmmssfff}", DateTime.Now)
  End Function

  Shared Sub CreateBackupFile(originalFilePath As String)
    If String.IsNullOrEmpty(originalFilePath) Then
      Return 'nothing to do
    End If

    Dim fi As FileInfo = New FileInfo(originalFilePath)
    If Not fi.Exists Then
      Return 'nothing to do
    End If
    Dim backupPath As String = WorkflowFileHelper.FindBackupFilesFolder()
    If String.IsNullOrEmpty(backupPath) Then
      Return 'nothing to do
    End If

    Dim backupFileFullPath As String = String.Format("{0}\{1}-{2}{3}",
      backupPath, Path.GetFileNameWithoutExtension(fi.Name), GetUniqueToken(), Path.GetExtension(fi.Name))
    fi.CopyTo(backupFileFullPath)
  End Sub

  Shared Sub CreateBackupFile(workflowFileName As String, xmlData As XDocument)
    If xmlData Is Nothing Then Return

    Dim backupPath As String = WorkflowFileHelper.FindBackupFilesFolder()
    If String.IsNullOrEmpty(backupPath) Then
      Return 'nothing to do
    End If
    Dim backupFileFullPath As String = String.Format("{0}\{1}-{2}.xml",
      backupPath, Path.GetFileNameWithoutExtension(workflowFileName), GetUniqueToken())
    xmlData.Save(backupFileFullPath)
  End Sub
End Class

''' <summary>A xml serializable version of the TimeSpan class</summary>
Public Structure XmlSerializableTimeSpan
  Implements IXmlSerializable
  Private _value As TimeSpan
  Public Shared Widening Operator CType(ByVal ts As TimeSpan) As XmlSerializableTimeSpan
    Return New XmlSerializableTimeSpan With {._value = ts}
  End Operator
  Public Shared Widening Operator CType(ByVal xmlts As XmlSerializableTimeSpan) As TimeSpan
    Return xmlts._value
  End Operator
  Public Function GetSchema() As System.Xml.Schema.XmlSchema Implements System.Xml.Serialization.IXmlSerializable.GetSchema
    Return Nothing
  End Function
  Public Sub ReadXml(ByVal reader As System.Xml.XmlReader) Implements System.Xml.Serialization.IXmlSerializable.ReadXml
    Dim strValue As String = reader.ReadElementContentAsString
    If Not String.IsNullOrEmpty(strValue) Then
      Try
        _value = TimeSpan.Parse(strValue)
      Catch ex As Exception
        _value = New System.TimeSpan(0)
      End Try
    Else
      _value = New System.TimeSpan(0)
    End If
  End Sub
  Public Sub WriteXml(ByVal writer As System.Xml.XmlWriter) Implements System.Xml.Serialization.IXmlSerializable.WriteXml
    writer.WriteValue(_value.ToString)
  End Sub
  Public ReadOnly Property TotalSeconds
    Get
      Return _value.TotalSeconds
    End Get
  End Property
  Overrides Function ToString() As String
    If _value.TotalSeconds > 0 Then
      Return _value.ToString()
    End If
    Return Nothing
  End Function
End Structure

''' <summary>The last used data of the application dialog</summary>
Public Class LastUsedData
  ''' <summary>The name of the workflow file (without the 'wf_' prefix and '.xml' extension)</summary>
  Public Property FileName As String
  ''' <summary>The input GN4 object ids of the workflow to execute</summary>
  Public Property ObjectIds As String
  ''' <summary>The input parameters of the workflow to execute</summary>
  Public Property Parameters As String
  ''' <summary>The input files of the workflow to execute</summary>
  Public Property Data As String()
  ''' <summary>The .eml email input file</summary>
  Public Property Email As String
  ''' <summary>The first comment line of the workflow file. Typically: '<!-- Miles33/Tera DP --&gt;'</summary>
  Public Property CommentHeader As String
  ''' <summary>Optional comments of the workflow file</summary>
  Public Property Comments As String
  ''' <summary>The list of referenced libraries</summary>
  Public Property ReferencesList As String()
  ''' <summary>The workflow description (used in log messages)</summary>
  Public Property Description As String
  ''' <summary>The workflow timeout (only for Navigator workflow)</summary>
  Public Property Timeout As XmlSerializableTimeSpan
  ''' <summary>If displaying or not the progressing messages</summary>
  ''' <remarks>Note that, in Navigator workflow, each interactive activity can have its own 'DisplayProgress' attribute</remarks>
  Public Property DisplayProgress As Nullable(Of Boolean) = Nothing
End Class

''' <summary>The general settings and the last used values of the Workflow Tester.</summary>
''' <remarks>These data are loaded at starting and saved when the application is closed</remarks>
Public Class TesterSettings
  ''' <summary>The name of the file where the settings are stored</summary>
  Public Const FileName As String = "WorkflowTester.ini"
  ''' <summary>The application settings</summary>
  Public Property Settings As SettingsData = New SettingsData()
  ''' <summary>True if Sequential workflow; False if Navigator workfow</summary>
  Public Property IsSequential As Boolean = True
  ''' <summary>The last used data of the sequential workflow dialog</summary>
  Public Property SequentialData As LastUsedData = New LastUsedData()
  ''' <summary>The last used data of the navigator workflow dialog</summary>
  Public Property NavigatorData As LastUsedData = New LastUsedData()

  Public Class SettingsData
    ''' <summary>The path of the folder containing the SequentialProgram.vb and NavigatorProgram.vb source files</summary>
    ''' <remarks>Used to generate the filanl workflow file at runtime</remarks> 
    Public Property SourceFilesFolder As String
    ''' <summary>The url of the GN4 web application</summary>
    ''' <remarks>Used to emulate a client for the execution of the Navigator workflow</remarks> 
    Public Property GN4Url As String
    ''' <summary>The user name of the GN4 web application, </summary>
    ''' <remarks>Used to emulate a client for the execution of the Navigator workflow</remarks> 
    Public Property UserName As String
    ''' <summary>The url of the GN4 web application</summary>
    ''' <remarks>Used to emulate a client for the execution of the Navigator workflow</remarks> 
    Public Property Password As String
  End Class

  ''' <summary>Xml serialization of the TesterSettings object</summary>
  ''' <returns>The serialized xml</returns>
  Public Function Serialize() As XDocument
    Dim result As XDocument = Nothing

    Using ms As MemoryStream = New MemoryStream()
      Using writer As XmlWriter = XmlWriter.Create(ms)
        Dim serializer As XmlSerializer = New XmlSerializer(Me.GetType())
        serializer.Serialize(writer, Me)
        result = New XDocument()
        ms.Seek(0, SeekOrigin.Begin)
        result = XDocument.Load(ms)
      End Using
    End Using
    Return result
  End Function

  ''' <summary>Xml de-serialization of the TesterSettings object</summary>
  ''' <returns>The created TesterSettings object</returns>
  Public Shared Function Deserialize(xData As XDocument) As TesterSettings
    If xData IsNot Nothing Then
      Using ms As MemoryStream = New MemoryStream()
        xData.Save(ms)
        ms.Seek(0, SeekOrigin.Begin)
        Using reader As XmlReader = XmlReader.Create(ms)
          Dim serializer As XmlSerializer = New XmlSerializer(GetType(TesterSettings))
          Return serializer.Deserialize(reader)
        End Using
      End Using
    End If
    Return New TesterSettings() 'return always a valid object
  End Function
End Class

Public Class ParameterItem
  Implements IEquatable(Of ParameterItem)

  Public Property Name As String
  Public Property Value As String

  Public Sub New(aName As String, aValue As String)
    Name = aName
    Value = aValue
  End Sub

  Public Overloads Function Equals(ByVal other As ParameterItem) _
    As Boolean Implements IEquatable(Of ParameterItem).Equals
    Return (String.Compare(Me.Name, other.Name, False) = 0)
  End Function
End Class

Public Class TextBoxWriter
  Inherits System.IO.TextWriter

  Private control As TextBoxBase
  Private Builder As StringBuilder

  Public Sub New(ByVal control As RichTextBox)
    Me.control = control
'TODO
    'control.OnApplyTemplate += New EventHandler(AddressOf OnHandleCreated)
  End Sub
  Public Overrides Sub Write(ByVal ch As Char)
    Write(ch.ToString())
  End Sub
  Public Overrides Sub Write(ByVal s As String)
    If (control.IsInitialized) Then
      AppendText(s)
    Else
      BufferText(s)
    End If
  End Sub
  Public Overrides Sub WriteLine(ByVal s As String)
    Write(s + Environment.NewLine)
  End Sub
  Private Sub BufferText(ByVal s As String)
    If (Builder Is Nothing) Then
      Builder = New StringBuilder()
    End If
    Builder.Append(s)
  End Sub
  Private Sub AppendText(ByVal s As String)
    If control.Dispatcher.CheckAccess Then
      AppendTextInternal(s)
    Else
      control.Dispatcher.Invoke(Sub()
        AppendTextInternal(s)
                                End Sub)
    End If
  End Sub
  Private Sub AppendTextInternal(ByVal s As String)
    If Builder IsNot Nothing Then
      control.AppendText(Builder.ToString())
      Builder = Nothing
    End If
    control.AppendText(s)
  End Sub
'TODO
  'Private Sub OnHandleCreated(ByVal sender As Object, ByVal e As EventArgs)
  '  If Builder IsNot Nothing Then
  '    AppendText(Builder.ToString())
  '    Builder = Nothing
  '  End If
  'End Sub
  Public Overrides ReadOnly Property Encoding() As System.Text.Encoding
    Get
      Return Encoding.Default
    End Get
  End Property
End Class

Public Class TesterProgress : Implements IProgress
#Region "Members"
  Private ProgTextBox As RichTextBox
  Private ProgMessage As String
  Private ProgNest As Integer
  Private NestString As String
  Private MessageFormat As String
  Private DoneFormat As String
  Private StepString As String
  Private bNewLine As Boolean
#End Region
#Region "Constructors"
  Public Sub New(progressTextBox As RichTextBox)
    Me.New("  ", progressTextBox)
  End Sub
  Public Sub New(nestString As String, progressTextBox As RichTextBox)
    ProgTextBox = progressTextBox
    ProgMessage = String.Empty
    ProgNest = 0
    nestString = nestString
    MessageFormat = "{0} "
    DoneFormat = " {0}"
    StepString = "."
    bNewLine = True
  End Sub
#End Region
#Region "Properties"
  Public ReadOnly Property Message As String Implements IProgress.Message
  Get
    Return ProgMessage
  End Get
  End Property

  Public ReadOnly Property Interrupted As Boolean Implements IProgress.Interrupted
  Get
    Return False
  End Get
  End Property
#End Region
#Region "Operations"
  Sub Setup(n As Integer, message As String, nSteps As Integer) Implements IProgress.Setup
    InternalSetup(n, message, nSteps, True)
  End Sub

  Sub OneStepSetup(message As String) Implements IProgress.OneStepSetup
    InternalSetup(1, message, 1, False)
  End Sub

  Sub InternalSetup(n As Integer, message As String, nSteps As Integer, nest As Boolean) Implements IProgress.InternalSetup
    If nSteps < 0 Then
      Throw New ArgumentException("nSteps")
    End If
    ProgMessage = message
    If Not bNewLine Then
      WriteMessage(System.Environment.NewLine)
      bNewLine = True
    End If
    For i As Integer = 0 To ProgNest Step 1
      WriteMessage(NestString)
    Next
    WriteMessage(String.Format(MessageFormat, ProgMessage))
    bNewLine = False
    If nest Then
      ProgNest += 1
    End If
  End Sub

  Sub EndProgress(n As Integer) Implements IProgress.End
    InternalEnd(n, False)
  End Sub

  Sub ErrorEnd(n As Integer) Implements IProgress.ErrorEnd
   InternalEnd(n, True)
  End Sub

  Sub InternalEnd(n As Integer, isError As Boolean) Implements IProgress.InternalEnd
    If ProgNest > 0 Then
      ProgNest -= 1
      While (n - 1) > 0
        n -= 1
        WriteMessage(NestString)
      End While
      If isError Then
        WriteLine(DoneFormat, "error")
      Else
        WriteLine(DoneFormat, "done")
      End If
      bNewLine = True
    End If
  End Sub

  Sub StepProgress(n As Integer) Implements IProgress.Step
    If ProgNest > 0 And n > 0 Then
      While (n - 1) > 0
        n -= 1
        WriteMessage(StepString)
      End While
      bNewLine = False
    End If
  End Sub

  Sub SendCode(code As Integer) Implements IProgress.SendCode
  End Sub

  Sub WriteMessage(message As String)
    If ProgTextBox IsNot Nothing And Not String.IsNullOrEmpty(message) Then
      If ProgTextBox.Dispatcher.CheckAccess Then
        ProgTextBox.AppendText(message)
      Else
        ProgTextBox.Dispatcher.Invoke(Sub()
          ProgTextBox.AppendText(message)
                                      End Sub)
      End If
    End If
  End Sub

  Sub WriteLine()
    WriteLine(Nothing)
  End Sub

  Sub WriteLineColor(message As String, textColor As SolidColorBrush, textWeight As FontWeight)
    If ProgTextBox IsNot Nothing Then
      If ProgTextBox.Dispatcher.CheckAccess Then
        WriteLineColorInternal(message, textColor, textWeight)
      Else
        ProgTextBox.Dispatcher.Invoke(Sub()
          WriteLineColorInternal(message, textColor, textWeight)
                                      End Sub)
      End If
    End If
  End Sub

  Sub WriteLineColorInternal(message As String, textColor As SolidColorBrush, textWeight As FontWeight)
    If ProgTextBox IsNot Nothing Then
      Dim rangeText As TextRange = New TextRange(ProgTextBox.Document.ContentEnd, ProgTextBox.Document.ContentEnd)
      rangeText.Text = message
      rangeText.ApplyPropertyValue(TextElement.ForegroundProperty, textColor)
      rangeText.ApplyPropertyValue(TextElement.FontWeightProperty, textWeight)
    End If
  End Sub

  Sub WriteLine(message As String)
    If ProgTextBox IsNot Nothing Then
      WriteMessage(message + System.Environment.NewLine)
    End If
  End Sub

  Sub WriteLine(message As String, ParamArray args As Object())
    If ProgTextBox IsNot Nothing Then
      WriteLine(String.Format(System.Globalization.CultureInfo.CurrentCulture, message, args))
    End If
  End Sub

  Sub WriteLineColor(textColor As SolidColorBrush, textWeight As FontWeight, message As String, ParamArray args As Object())
    If ProgTextBox IsNot Nothing Then
      WriteLineColor(String.Format(System.Globalization.CultureInfo.CurrentCulture, message, args), textColor, textWeight)
    End If
  End Sub
#End Region
End Class

Partial Public Class SequentialWorkflow
  Private Context As SequentialExecutionContext
  Private Utils As WFUtils
  Private Sub New(ByVal aContext As SequentialExecutionContext)
    Context = aContext
    Utils = New WFUtils(aContext)
  End Sub
  Public Shared Sub __Execute(ByVal aContext As SequentialExecutionContext)
    Dim wf As New SequentialWorkflow(aContext)
    wf.__Do()
  End Sub
End Class

Partial Public Class NavigatorWorkflow
  Private Context As NavigatorExecutionContext
  Private Utils As WFUtils
  Private ReadOnly wfManager As TeraDP.GN4.WinUI.WorkflowManager = New TeraDP.GN4.WinUI.WorkflowManager()

  Private Sub New(ByVal aContext As NavigatorExecutionContext)
    Context = aContext
    Utils = New WFUtils(aContext)
    'emulate the login of a client application
    Dim clientLogin As TeraDP.GN4.Client.ClientLogin = New TeraDP.GN4.Client.ClientLogin(
      WFTest.ServerUrl, Nothing, Nothing, Nothing, Nothing, WFTest.ClientUserName, WFTest.ClientUserPassword, Nothing, 0)
    Dim dc As TeraDP.GN4.WinUI.DataConnection = TeraDP.GN4.WinUI.DataConnection.Create(clientLogin)
    Context.ShowInteractionMethod = AddressOf ShowInteraction
  End Sub
  Public Shared Sub __Execute(ByVal aContext As NavigatorExecutionContext)
    Dim wf As New NavigatorWorkflow(aContext)
    Dim task As System.Threading.Tasks.Task = wf.__Do()
    If Not task Is Nothing AndAlso task.IsFaulted Then
      If Not task.Exception.InnerException Is Nothing Then
        Throw task.Exception.InnerException
      End If
    End If
  End Sub
  Public Sub ShowInteraction(ByVal restResult As TeraDP.GN4.Workflow.InterRestResult)
    wfManager.ShowInteraction(Context.InstanceId, restResult)
  End Sub
End Class

''' <summary>Some utility methods</summary>
Public Class Utility

  ''' <summary>Return the input string with a '/' as last character (or empty)</summary>
  ''' <param name="Str">The string to check</param>
  ''' <returns>The modified string</returns>
  Public Shared Function EnsureLastSlash(str As String) As String
    If Not String.IsNullOrEmpty(str) Then
      Dim tempStr As String = str.TrimEnd({"/"c, "\"c, " "c})
      If Not String.IsNullOrEmpty(tempStr) Then
        Return tempStr & "/"
      Else
        Return String.Empty
      End If
    End If
    Return str
  End Function

  ''' <summary>Return the internal exception contained into the input one</summary>
  ''' <param name="ex">The input exception</param>
  ''' <returns>The internal exception</returns>
  Public Shared Function GetInternalException(ex As Exception) As Exception
    While ex.InnerException IsNot Nothing
      ex = ex.InnerException
    End While
    Return ex
  End Function

  Public Shared Sub SetMargins(ByRef control As Control, left As Integer, top As Integer, right As Integer, bottom As Integer)
    Dim margin As Thickness = control.Margin
    margin.Left = left
    margin.Top = top
    margin.Right = right
    margin.Bottom = bottom
    control.Margin = margin
  End Sub

  Public Shared Function NameValuesFromString(str As String) AS NameValueCollection
    Return NameValuesFromString(str, ":"c, ";"c)
  End Function

  Private Shared Function NameValuesFromString(str As String, itemSeparator As Char, listSeparator As Char) AS NameValueCollection
    If String.IsNullOrEmpty(str) Then
      Return Nothing
    End If

    Dim result As NameValueCollection = new NameValueCollection()
    Dim escaped As Boolean = false
    Dim i As Integer = 0
    While i < str.Length
      Dim j As Integer = i

      escaped = false
      While (i < str.Length)
        If Not escaped Then
          If str.Chars(i) = "\"c Then
            escaped = True
          Else If str.Chars(i) = itemSeparator Then
            Exit While            
          End If
        Else
          escaped = False
        End If
        i += 1
      End While

      If i <= j Then
        Throw New FormatException()
      End If
      Dim name As String = str.Substring(j, i - j)
      name = name.Replace(String.Format("\{0}", itemSeparator), itemSeparator.ToString()).Replace("\\", "\")
      If i < str.Length Then
        i += 1
      End If
      j = i

      escaped = false
      While (i < str.Length)
        If Not escaped Then
          If str.Chars(i) = "\"c Then
            escaped = True
          Else If str.Chars(i) = listSeparator Then
            Exit While            
          End If
        Else
          escaped = False
        End If
        i += 1
      End While

      Dim value As String = str.Substring(j, i - j)
      value = value.Replace(String.Format("\{0}", listSeparator), listSeparator.ToString()).Replace("\\", "\")
      result.Add(name, value)
      If i < str.Length Then
        i += 1
      End If
    End While
    Return result
  End Function

  Public Shared Function NameValuesToString(values As NameValueCollection) As String
    If values Is Nothing OrElse values.Count = 0 Then
      Return String.Empty
    End If

    Dim result As StringBuilder = New StringBuilder()
    For i As Integer = 0 To values.Count - 1
      Dim key As String = values.GetKey(i).Replace("\","\\").Replace(":","\:")
      If key.Length > 0 Then
        For Each value As String in values.GetValues(i)
          result.AppendFormat("{0}:{1};", key, value.Replace("\","\\").Replace(";","\;"))
        Next
      End If
    Next
    return result.ToString()
  End Function

End Class
