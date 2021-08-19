'Miles33/TeraDP
Imports System.Collections.Specialized
Imports System.Text
Imports TeraDP.GN4.GNClient
Imports TeraDP.GN4.Server

Class MainWindow

Public CommentsHeaderDefault = "Miles33/Tera DP"
Public Shared WorkflowResult As Workflow.Result = Nothing
Public Shared ResultMessage As String

'''<summary>The info, warning and error icons</summary>
Public Shared LogIcons As List(Of ImageSource) = New List(Of ImageSource)

Public ParameterList As List(Of ParameterItem) = New List(Of ParameterItem)
Public ApplicationSettings As TesterSettings = Nothing
Public writeResultMethod As TeraDP.GN4.Server.WFTest.WriteResultDelegate = AddressOf WriteResult


Public Sub New()
  'This call is required by the designer.
  InitializeComponent()
  ParametersListView.ItemsSource = ParameterList

  LogIcons.Add(CType(Me.TryFindResource("info"), ImageSource))
  LogIcons.Add(CType(Me.TryFindResource("warning"), ImageSource))
  LogIcons.Add(CType(Me.TryFindResource("error"), ImageSource))

  Try
    Dim aName As System.Reflection.AssemblyName = New Reflection.AssemblyName(GetType(SequentialExecutionContext).Assembly.FullName)
    Dim dbName As String = Configuration.ConfigurationManager.AppSettings("Db.Name")
    Dim title As String = String.Format(" ({0})", aName.Version.ToString())
    If Not String.IsNullOrEmpty(dbName) Then
      title &= String.Format(" - {0}", dbName)
    End If
    Me.Title &= title
  Catch ex As Exception
    'continue 
  End Try

  LoadSettings()
  AddHandler SequentialRadioButton.Checked, AddressOf SequentialRadioButton_Checked
  AddHandler NavigatorRadioButton.Checked, AddressOf NavigatorRadioButton_Checked
  WorkflowFileHelper.MainWnd = Me
  ObjectIdsTextBox.Focus()
End Sub

Private Sub Window_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
  SaveSettings(SequentialRadioButton.IsChecked)
End Sub

Private Sub MainTabControl_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles MainTabControl.SelectionChanged
  Dim forceGenerateWorkflow As Boolean = False

  If e IsNot Nothing Then
    'lost focus
    If e.RemovedItems.Count > 0 Then
      If TypeOf e.RemovedItems(0) Is TabItem Then
        Dim fromTabItem As TabItem = e.RemovedItems(0)
        If String.Compare(fromTabItem.Header, "Parameters", True) = 0 _
           OrElse String.Compare(fromTabItem.Header, "Settings", True) = 0 Then
          SaveSettings(SequentialRadioButton.IsChecked)
        ElseIf String.Compare(fromTabItem.Header, "References", True) = 0 Then
          SaveSettings(SequentialRadioButton.IsChecked)
          forceGenerateWorkflow = True 'the comments can be changed
        End If
      End If
    End If

    'got focus
    If e.AddedItems.Count > 0 Then
      If TypeOf e.AddedItems(0) Is TabItem Then
        Dim toTabItem As TabItem = e.AddedItems(0)
        If String.Compare(toTabItem.Header, "Settings", True) = 0 Then
          'set default settings 
          If String.IsNullOrEmpty(SettingsGN4UrlTextBox.Text) Then
            SettingsGN4UrlTextBox.Text = "http://localhost/gn4/"
          End If
          GetSourceFolderPath()
        ElseIf String.Compare(toTabItem.Header, "Workflow", True) = 0 _
           OrElse String.Compare(toTabItem.Header, "File", True) = 0 Then
          'generate the final workflow file
          ComposeWorkflowFile(forceGenerateWorkflow)
        ElseIf String.Compare(toTabItem.Header, "References", True) = 0 Then
          If String.IsNullOrEmpty(CommentTextBox.Text) Then
            CommentTextBox.Text = CommentsHeaderDefault
          End If
          TimeoutTextBox.IsEnabled = Not SequentialRadioButton.IsChecked
        ElseIf String.Compare(toTabItem.Header, "Database", True) = 0 Then
          Dim dbTabItem As DatabaseTabItem = CType(toTabItem, DatabaseTabItem)
          If dbTabItem IsNot Nothing Then
            dbTabItem.LoadDataAsync()
          End If
        End If
      End If
    End If
  End If
End Sub

Public Function GetFileName() As String
  If String.IsNullOrEmpty(SettingsSolutionFolderTextBox.Text) Then
    'try to find the folder containing the source files (SequentialProgram.vb and NavigatorProgram.vb)
    Dim sourceFileFolder As String = WorkflowFileHelper.FindSourceFilesFolder()
    If Not String.IsNullOrEmpty(sourceFileFolder) Then
      SettingsSolutionFolderTextBox.Text = sourceFileFolder
    End If
  End If
  Return SettingsSolutionFolderTextBox.Text
End Function

Public Function GetSourceFolderPath() As String
  If String.IsNullOrEmpty(SettingsSolutionFolderTextBox.Text) Then
    'try to find the folder containing the source files (SequentialProgram.vb and NavigatorProgram.vb)
    Dim sourceFileFolder As String = WorkflowFileHelper.FindSourceFilesFolder()
    If Not String.IsNullOrEmpty(sourceFileFolder) Then
      SettingsSolutionFolderTextBox.Text = sourceFileFolder
    End If
  End If
  Return SettingsSolutionFolderTextBox.Text
End Function

Private Sub LoadSettings()
  If Not System.IO.File.Exists(TesterSettings.FileName) Then
    ApplicationSettings = New TesterSettings()
    Return 'nothing to do
  End If

  Try
    Dim xSettings As XDocument = XDocument.Load(TesterSettings.FileName)
    ApplicationSettings = TesterSettings.Deserialize(xSettings)

    If ApplicationSettings.IsSequential Then
      SequentialRadioButton.IsChecked = True
    Else
      NavigatorRadioButton.IsChecked = True
    End If
    SetSettings(ApplicationSettings.IsSequential)
  Catch ex As Exception
    MsgBox(String.Format("Cannot load setting from '{0}' file: {1}", TesterSettings.FileName, Utility.GetInternalException(ex).Message),
      MsgBoxStyle.Exclamation And MsgBoxStyle.OkOnly,
      "Loading settings")
  End Try
End Sub

Private Sub SetSettings(isSequential As Boolean)
  'read last used values
  Dim lastUsed As LastUsedData = Nothing
  If isSequential Then
    lastUsed = ApplicationSettings.SequentialData
  Else
    lastUsed = ApplicationSettings.NavigatorData
  End If

  FileNameTextBox.Text = lastUsed.FileName
  If String.IsNullOrEmpty(FileNameTextBox.Text) Then
    FileNameTextBox.Text = WorkflowFileHelper.GetCleanFileName(Nothing)
  End If
  ObjectIdsTextBox.Text = lastUsed.ObjectIds
  Dim ids() As IdVersion = Nothing
  AreValidIds(ids)

  Dim parameters As NameValueCollection = Utility.NameValuesFromString(lastUsed.Parameters)
  ParameterList.Clear()
  If parameters IsNot Nothing AndAlso parameters.Count > 0 Then
    For Each key As String In parameters.Keys
      Dim item As ParameterItem = New ParameterItem(key, parameters(key))
      ParameterList.Add(item)
    Next
  End If
  ParametersListView.Items.Refresh()
  ParametersListView.SelectedItem = Nothing
  SetParametersHeader()

  DataListView.Items.Clear()
  If lastUsed.Data IsNot Nothing AndAlso lastUsed.Data.Count > 0 Then
    For Each filePath As String In lastUsed.Data
      DataListView.Items.Add(filePath)
    Next
  End If
  SetDataHeader()

  EmailTextBox.Text = lastUsed.Email
  SetEmailHeader()

  'load comments
  CommentTextBox.Text = lastUsed.CommentHeader
  CommentRichTextBox.Document.Blocks.Clear()
  CommentRichTextBox.Document.LineHeight = 1
  If Not String.IsNullOrEmpty(lastUsed.Comments) Then
    CommentRichTextBox.AppendText(lastUsed.Comments)
  End If

  'load references
  ReferencesListView.Items.Clear()
  If lastUsed.ReferencesList IsNot Nothing AndAlso lastUsed.ReferencesList.Count > 0 Then
    For Each item As String In lastUsed.ReferencesList
      ReferencesListView.Items.Add(item)
    Next
  End If
  SetReferencesHeader()

  'load workflow attributes
  DescriptionTextBox.Text = lastUsed.Description
  If lastUsed.DisplayProgress Is Nothing OrElse lastUsed.DisplayProgress = True Then
    DisplayProgressRadioButton.IsChecked = True
  Else
    NoProgressRadioButton.IsChecked = True
  End If
  TimeoutTextBox.Text = Nothing
  If isSequential Then
    TimeoutTextBox.IsEnabled = False
  Else
    If lastUsed.Timeout.TotalSeconds > 0 Then
      TimeoutTextBox.Text = lastUsed.Timeout.ToString()
    End If
    TimeoutTextBox.IsEnabled = True
  End If

  'read settings
  SettingsGN4UrlTextBox.Text = Utility.EnsureLastSlash(ApplicationSettings.Settings.GN4Url)
  SettingsUserNameTextBox.Text = ApplicationSettings.Settings.UserName
  SettingsPasswordTextBox.Text = ApplicationSettings.Settings.Password
  If Not String.IsNullOrEmpty(ApplicationSettings.Settings.SourceFilesFolder) Then
    SettingsSolutionFolderTextBox.Text = ApplicationSettings.Settings.SourceFilesFolder
  End If
End Sub

Private Sub SaveSettings(isSequential As Boolean)
  Try
    'save last used values
    ApplicationSettings.IsSequential = isSequential

    If ApplicationSettings.IsSequential Then
      ApplicationSettings.SequentialData.FileName = FileNameTextBox.Text
      ApplicationSettings.SequentialData.ObjectIds = ObjectIdsTextBox.Text
      ApplicationSettings.SequentialData.Email = EmailTextBox.Text
    Else
      ApplicationSettings.NavigatorData.FileName = FileNameTextBox.Text
      ApplicationSettings.NavigatorData.ObjectIds = ObjectIdsTextBox.Text
      ApplicationSettings.NavigatorData.Email = EmailTextBox.Text
    End If

    Dim parameters As NameValueCollection = Nothing
    If ParameterList IsNot Nothing And ParameterList.Count > 0 Then
      parameters = New System.Collections.Specialized.NameValueCollection()
      For Each item As ParameterItem In ParameterList
        parameters.Add(item.Name, item.Value)
      Next
    End If
    If ApplicationSettings.IsSequential Then
      ApplicationSettings.SequentialData.Parameters = Utility.NameValuesToString(parameters)
    Else
      ApplicationSettings.NavigatorData.Parameters = Utility.NameValuesToString(parameters)
    End If

    Dim files As List(Of String) = New List(Of String)()
    If DataListView.Items.Count > 0 Then
      For Each filePath As String In DataListView.Items
        files.Add(filePath)
      Next
    End If
    If ApplicationSettings.IsSequential Then
      ApplicationSettings.SequentialData.Data = files.ToArray()
    Else
      ApplicationSettings.NavigatorData.Data = files.ToArray()
    End If

    'save comments
    Dim comments As String = New TextRange(CommentRichTextBox.Document.ContentStart, CommentRichTextBox.Document.ContentEnd).Text
    If ApplicationSettings.IsSequential Then
      ApplicationSettings.SequentialData.CommentHeader = CommentTextBox.Text
      ApplicationSettings.SequentialData.Comments = comments
    Else
      ApplicationSettings.NavigatorData.CommentHeader = CommentTextBox.Text
      ApplicationSettings.NavigatorData.Comments = comments
    End If

    'save references
    Dim referencesList As List(Of String) = New List(Of String)()
    If ReferencesListView.Items.Count > 0 Then
      For Each item As String In ReferencesListView.Items
        referencesList.Add(item)
      Next
    End If
    If ApplicationSettings.IsSequential Then
      ApplicationSettings.SequentialData.ReferencesList = referencesList.ToArray()
    Else
      ApplicationSettings.NavigatorData.ReferencesList = referencesList.ToArray()
    End If

    'save workflow attributes
    If ApplicationSettings.IsSequential Then
      ApplicationSettings.SequentialData.Description = DescriptionTextBox.Text
      ApplicationSettings.SequentialData.DisplayProgress = DisplayProgressRadioButton.IsChecked
      ApplicationSettings.SequentialData.Timeout = Nothing 'only for Navigator
    Else
      ApplicationSettings.NavigatorData.Description = DescriptionTextBox.Text
      ApplicationSettings.NavigatorData.DisplayProgress = DisplayProgressRadioButton.IsChecked
      ApplicationSettings.NavigatorData.Timeout = Nothing
      If Not String.IsNullOrEmpty(TimeoutTextBox.Text) Then
        Try
          Dim tmo As TimeSpan = TimeSpan.Parse(TimeoutTextBox.Text)
          If tmo.TotalSeconds > 0 Then
            ApplicationSettings.NavigatorData.Timeout = tmo
          End If
        Catch
          'continue with no exception
        End Try
      End If
    End If

    'save settings
    ApplicationSettings.Settings.GN4Url = Utility.EnsureLastSlash(SettingsGN4UrlTextBox.Text)
    ApplicationSettings.Settings.UserName = SettingsUserNameTextBox.Text
    ApplicationSettings.Settings.Password = SettingsPasswordTextBox.Text
    ApplicationSettings.Settings.SourceFilesFolder = GetSourceFolderPath()

    Dim xSettings As XDocument = ApplicationSettings.Serialize()
    xSettings.Save(TesterSettings.FileName)
  Catch ex As Exception
    MsgBox(String.Format("Cannot save setting to '{0}' file: {1}", TesterSettings.FileName, Utility.GetInternalException(ex).Message),
      MsgBoxStyle.Exclamation And MsgBoxStyle.OkOnly,
      "Save settings")
  End Try
End Sub

Private Sub ExecuteButton_Click(sender As Object, e As RoutedEventArgs)
  SaveSettings(SequentialRadioButton.IsChecked)

  'clear error labels
  ParametersErrorTextBox.Text = Nothing
  ParametersErrorTextBox.Visibility = System.Windows.Visibility.Collapsed

  'read input ObjectIds
  Dim ids() As IdVersion = Nothing
  If Not AreValidIds(ids) Then
    Return
  End If

  'read input parameters
  Dim parameters As System.Collections.Specialized.NameValueCollection = Nothing
  If ParameterList IsNot Nothing And ParameterList.Count > 0 Then
    parameters = New System.Collections.Specialized.NameValueCollection()
    For Each item As ParameterItem In ParameterList
      parameters.Add(item.Name, item.Value)
    Next
  End If

  'read the path of the binary files
  Dim files() As String = Nothing
  If DataListView.Items.Count > 0 Then
    Dim fileList As List(Of String) = New List(Of String)()
    For Each filePath As String In DataListView.Items
      fileList.Add(filePath)
    Next
    If fileList.Count > 0 Then
      files = fileList.ToArray()
    End If
  End If

  ExecuteButton.IsEnabled = False
  ExecuteButton.Content = "Executing..."
  Dim result As Task(Of Boolean) = RunWorkflowAsync(SequentialRadioButton.IsChecked, ids, parameters, files)
End Sub

Private Function AreValidIds(ByRef ids() As IdVersion) As Boolean
  ids = Nothing
  Dim objectIdsCount As String = Nothing
  'clear error label
  ObjectIdsErrorTextBox.Text = Nothing
  ObjectIdsErrorTextBox.Visibility = System.Windows.Visibility.Collapsed

  Try
    ids = StringUtility.IdVersionArrayFromString(ObjectIdsTextBox.Text.TrimEnd())
    If ids IsNot Nothing AndAlso ids.Count > 0 Then
      objectIdsCount = String.Format(" ({0})", ids.Count)
    End If
    Return True 'valid
  Catch ex As Exception
    ObjectIdsErrorTextBox.Text = String.Format("Invalid object id: {0}", ex.Message)
    ObjectIdsErrorTextBox.Visibility = System.Windows.Visibility.Visible
    Return False 'not valid
  Finally
    ObjectIdsGroupBox.Header = String.Format("Object Ids{0}", objectIdsCount)
  End Try
End Function

Private Sub ObjectIdsTextBox_LostFocus(sender As Object, e As RoutedEventArgs)
  'verify the ids
  Dim ids() As IdVersion = Nothing
  AreValidIds(ids)
End Sub

Private Sub ObjectIdsClearButton_Click(sender As Object, e As RoutedEventArgs) Handles ObjectIdsClearButton.Click
  ObjectIdsTextBox.Text = Nothing
  Dim ids() As IdVersion = Nothing
  AreValidIds(ids)
End Sub

Private Sub ParametersAddButton_Click(sender As Object, e As RoutedEventArgs) Handles ParametersAddButton.Click
  ParametersErrorTextBox.Text = Nothing
  ParametersErrorTextBox.Visibility = System.Windows.Visibility.Collapsed

  Dim parName As String = ParametersNameTextBox.Text
  Dim parValue As String = ParametersValueTextBox.Text

  If String.IsNullOrEmpty(parName) Then
    ParametersErrorTextBox.Text = "Name cannot be empty"
    ParametersErrorTextBox.Visibility = System.Windows.Visibility.Visible
    Return
  End If
  'If String.IsNullOrEmpty(parValue) Then
  '  ParametersErrorTextBox.Text = "Value cannot be empty"
  '  ParametersErrorTextBox.Visibility = System.Windows.Visibility.Visible
  '  Return
  'End If

  Dim item As ParameterItem = New ParameterItem(parName, parValue)
  Dim idx As Integer = ParameterList.IndexOf(item)
  If idx > -1 Then
    ParameterList(idx) = item
  Else
    ParameterList.Add(item)
  End If
  ParametersListView.Items.Refresh()
  ParametersListView.SelectedItem = Nothing
  ParametersListView_SelectionChanged(ParametersListView, Nothing)
  SetParametersHeader()
End Sub

Private Sub ParametersRemoveButton_Click(sender As Object, e As RoutedEventArgs) Handles ParametersRemoveButton.Click
  ParametersErrorTextBox.Text = Nothing
  ParametersErrorTextBox.Visibility = System.Windows.Visibility.Collapsed

  Dim selItem As ParameterItem = Nothing
  If ParametersListView.SelectedItems.Count > 0 Then
    selItem = ParametersListView.SelectedItems(0)
  End If
  If selItem IsNot Nothing Then
    ParameterList.Remove(selItem)
    ParametersListView.Items.Refresh()
    ParametersListView.SelectedItem = Nothing
  End If
  SetParametersHeader()
End Sub

Private Sub ParametersListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ParametersListView.SelectionChanged
  Dim selItem As ParameterItem = Nothing
  If ParametersListView.SelectedItems.Count > 0 Then
    selItem = ParametersListView.SelectedItems(0)
  End If
  If selItem IsNot Nothing Then
    ParametersNameTextBox.Text = selItem.Name
    ParametersValueTextBox.Text = selItem.Value
    ParametersRemoveButton.IsEnabled = True
  Else
    ParametersNameTextBox.Text = Nothing
    ParametersValueTextBox.Text = Nothing
    ParametersRemoveButton.IsEnabled = False
  End If
End Sub

Private Sub ParametersClearButton_Click(sender As Object, e As RoutedEventArgs) Handles ParametersClearButton.Click
  ParameterList.Clear()
  ParametersListView.Items.Refresh()
  ParametersListView.SelectedItem = Nothing
  SetParametersHeader()
End Sub

Private Sub SetParametersHeader()
  Dim parametersCount As String = Nothing
  If ParameterList.Count > 0 Then
    parametersCount = String.Format(" ({0})", ParameterList.Count)
  End If
  ParametersGroupBox.Header = String.Format("Parameters{0}", parametersCount)
End Sub

Private Sub DataBrowse_Click(sender As Object, e As RoutedEventArgs) Handles DataBrowse.Click
  Dim dlg As Microsoft.Win32.OpenFileDialog = New Microsoft.Win32.OpenFileDialog()
  dlg.Multiselect = True
  dlg.Filter = "All files (*.*)|*.*"
  dlg.FilterIndex = 1
  Dim result As Nullable(Of Boolean) = dlg.ShowDialog()
  If Not result Then
    Return
  End If
  If dlg.FileNames IsNot Nothing AndAlso dlg.FileNames.Length > 0 Then
    For Each fileName As String In dlg.FileNames
      If Not DataListView.Items.Contains(fileName) Then
        DataListView.Items.Add(fileName)
      End If
    Next
  End If
  DataListView.SelectedItem = Nothing
  SetDataHeader()
End Sub

Private Sub DataRemoveButton_Click(sender As Object, e As RoutedEventArgs) Handles DataRemoveButton.Click
  'remove selected items
  While DataListView.SelectedItems.Count > 0
    Dim idx = DataListView.Items.IndexOf(DataListView.SelectedItem)
    DataListView.Items.RemoveAt(idx)
  End While
  SetDataHeader()
End Sub

Private Sub DataListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles DataListView.SelectionChanged
  Dim selItem As String = Nothing
  If DataListView.SelectedItems.Count > 0 Then
    selItem = DataListView.SelectedItems(0)
  End If
  DataRemoveButton.IsEnabled = Not String.IsNullOrEmpty(selItem)
End Sub

Private Sub DataClearButton_Click(sender As Object, e As RoutedEventArgs) Handles DataClearButton.Click
  DataListView.Items.Clear()
  SetDataHeader()
End Sub

Private Sub SetDataHeader()
  Dim dataCount As String = Nothing
  If DataListView.Items.Count > 0 Then
    dataCount = String.Format(" ({0})", DataListView.Items.Count)
  End If
  DataGroupBox.Header = String.Format("Data{0}", dataCount)
End Sub

Private Sub EmailBrowse_Click(sender As Object, e As RoutedEventArgs) Handles EmailBrowse.Click
  Dim dlg As Microsoft.Win32.OpenFileDialog = New Microsoft.Win32.OpenFileDialog()
  dlg.Multiselect = False
  dlg.Filter = "Email files (*.eml)|*.eml"
  dlg.FilterIndex = 1
  Dim result As Nullable(Of Boolean) = dlg.ShowDialog()
  If Not result Then
    Return
  End If
  If dlg.FileNames IsNot Nothing AndAlso dlg.FileNames.Length > 0 Then
    EmailTextBox.Text = dlg.FileNames(0)
  Else
    EmailTextBox.Text = Nothing
  End If
  SetEmailHeader()
End Sub

Private Sub EmailClearButton_Click(sender As Object, e As RoutedEventArgs) Handles EmailClearButton.Click
  EmailTextBox.Text = Nothing
  SetEmailHeader()
End Sub

Private Sub SetEmailHeader()
  If String.IsNullOrEmpty(EmailTextBox.Text) Then
    EmailGroupBox.Header = "Email"
  Else
    EmailGroupBox.Header = "Email (1)"
  End If
End Sub

Private Sub ProgressClipboardButton_Click(sender As Object, e As RoutedEventArgs) Handles ProgressClipboardButton.Click
  Dim sb As StringBuilder = New StringBuilder()
  sb.AppendLine("Messages:")
  sb.AppendLine(New TextRange(ProgressRichTextBox.Document.ContentStart, ProgressRichTextBox.Document.ContentEnd).Text)
  sb.AppendLine("Logs:")
  sb.AppendLine(ResultTextBox.Text)
  If WorkflowResult IsNot Nothing AndAlso WorkflowResult.Log IsNot Nothing Then
    For Each item As LogEntry In WorkflowResult.Log
      sb.AppendLine(String.Format("[{0}] {1}", item.Code.ToString(), item.Message))
    Next
  End If
  Clipboard.SetText(sb.ToString())
End Sub

Private Sub OutputClipboardButton_Click(sender As Object, e As RoutedEventArgs) Handles OutputClipboardButton.Click
  Clipboard.SetText(New TextRange(OutputRichTextBox.Document.ContentStart, OutputRichTextBox.Document.ContentEnd).Text)
End Sub

Private Sub WorkflowClipboardButton_Click(sender As Object, e As RoutedEventArgs) Handles WorkflowClipboardButton.Click
  Clipboard.SetText(New TextRange(WorkflowRichTextBox.Document.ContentStart, WorkflowRichTextBox.Document.ContentEnd).Text)
End Sub

Private Sub SolutionBrowse_Click(sender As Object, e As RoutedEventArgs) Handles SolutionBrowse.Click
  Dim dlg As System.Windows.Forms.FolderBrowserDialog = New System.Windows.Forms.FolderBrowserDialog()
  dlg.ShowNewFolderButton = False
  dlg.RootFolder = Environment.SpecialFolder.MyComputer
  dlg.Description = "Select the folder containing the SequentialProgram.vb and NavigatorProgram.vb source files."
  Dim result As System.Windows.Forms.DialogResult = dlg.ShowDialog()
  If result = Forms.DialogResult.OK Then
    SettingsSolutionFolderTextBox.Text = dlg.SelectedPath
    'reset the workflow tab
    WorkflowRichTextBox.Document.Blocks.Clear()
    WorkflowFileHelper.IsSequential = Nothing
  End If
End Sub

Private Sub ComposeWorkflowFile(force As Boolean)
  'generate the final workflow file
  Dim workflowHelper As WorkflowFileHelper = New WorkflowFileHelper(GetSourceFolderPath())

  Dim generateFile As Boolean = force
  If WorkflowFileHelper.IsSequential Is Nothing _
     Or (Not WorkflowFileHelper.IsSequential = SequentialRadioButton.IsChecked) Then
    generateFile = True 'generate the file only when it is the first time or different workflow type
  End If
  If generateFile Then
    Dim comments As String = New TextRange(CommentRichTextBox.Document.ContentStart, CommentRichTextBox.Document.ContentEnd).Text
    Dim referencesList As List(Of String) = New List(Of String)
    If ReferencesListView.Items.Count > 0 Then
      For Each item As String In ReferencesListView.Items
        referencesList.Add(item)
      Next
    End If
    Dim timeout As TimeSpan
    Try
      Dim tmo As TimeSpan = TimeSpan.Parse(TimeoutTextBox.Text)
      If tmo.TotalSeconds > 0 Then
        timeout = tmo
      End If
    Catch
      'continue with no exception
    End Try
    Dim wfString As String = workflowHelper.ComposeWorkflowString(SequentialRadioButton.IsChecked, CommentTextBox.Text, comments, referencesList,
      DescriptionTextBox.Text, DisplayProgressRadioButton.IsChecked, timeout)

    WorkflowRichTextBox.Document.Blocks.Clear()
    WorkflowRichTextBox.Document.LineHeight = 1
    If Not String.IsNullOrEmpty(wfString) Then
      WorkflowRichTextBox.AppendText(wfString)
    End If
  End If
End Sub

Private Sub WorkflowSaveButton_Click(sender As Object, e As RoutedEventArgs) Handles WorkflowSaveButton.Click
  Try
    Dim workflowHelper As WorkflowFileHelper = New WorkflowFileHelper(GetSourceFolderPath())
    workflowHelper.SaveFile(FileNameTextBox.Text, New TextRange(WorkflowRichTextBox.Document.ContentStart, WorkflowRichTextBox.Document.ContentEnd).Text)
  Catch ex As Exception
    OnError("Saving file", ex)
  End Try
End Sub

Private Sub WorkflowLoadButton_Click(sender As Object, e As RoutedEventArgs) Handles WorkflowLoadButton.Click
  Dim workflowFileName As String = FileNameTextBox.Text
  Dim workflowHelper As WorkflowFileHelper = New WorkflowFileHelper(GetSourceFolderPath())
  Dim xLoadedFile As XDocument = workflowHelper.LoadFile(workflowFileName)
  If xLoadedFile Is Nothing Then
    Return
  End If
  LoadWorkflow(workflowFileName, xLoadedFile, workflowHelper)
End Sub

Public Sub LoadWorkflow(workflowFileName As String, xmlData As XDocument)
  LoadWorkflow(workflowFileName, xmlData, New WorkflowFileHelper(GetSourceFolderPath()))
End Sub

Private Sub LoadWorkflow(workflowFileName As String, xmlData As XDocument, workflowHelper As WorkflowFileHelper)
  If xmlData Is Nothing Then Return
  If workflowHelper Is Nothing Then Return

  Try
    Dim isSequential As Boolean = True
    Dim commentsList As List(Of String) = Nothing
    Dim referencesList As List(Of String) = Nothing
    Dim importsList As List(Of String) = Nothing
    Dim membersCode As String = Nothing
    Dim workflowCode As String = Nothing
    Dim description As String = Nothing
    Dim displayProgress As Nullable(Of Boolean) = Nothing
    Dim timeout As TimeSpan

    'read the workflow xml
    workflowHelper.ParseWorkflowDocument(xmlData, isSequential, commentsList, referencesList, importsList, membersCode, workflowCode,
      description, displayProgress, timeout)
    If isSequential Then
      SequentialRadioButton.IsChecked = True
    Else
      NavigatorRadioButton.IsChecked = True
    End If
    FileNameTextBox.Text = workflowFileName

    'generate the .vb file
    Dim newVbText As String = workflowHelper.ComposeSourceFile(isSequential, importsList, membersCode, workflowCode)

    'set the references
    ReferencesTextBox.Clear()
    ReferencesListView.Items.Clear()
    ReferencesListView.SelectedItem = Nothing
    If referencesList IsNot Nothing AndAlso referencesList.Count > 0 Then
      For Each assemblyStr As String In referencesList
        ReferencesListView.Items.Add(assemblyStr)
      Next
    End If
    SetReferencesHeader()

    'set the comments
    CommentTextBox.Text = CommentsHeaderDefault
    CommentRichTextBox.Document.Blocks.Clear()
    CommentRichTextBox.Document.LineHeight = 1
    If commentsList IsNot Nothing AndAlso commentsList.Count > 0 Then
      If commentsList.Count > 1 Then
        CommentTextBox.Text = commentsList(0)
        CommentRichTextBox.AppendText(commentsList(1))
      End If
    End If

    'set workflow attributes
    DescriptionTextBox.Text = description
    If displayProgress Is Nothing OrElse displayProgress = True Then
      DisplayProgressRadioButton.IsChecked = True
    Else
      NoProgressRadioButton.IsChecked = True
    End If
    If (Not isSequential) AndAlso timeout.TotalSeconds > 0 Then
      TimeoutTextBox.Text = timeout.ToString()
    End If

    'create a backup of the SequentialProgram.vb or NavigatorProgram.vb source file
    Dim vbFilePath As String = workflowHelper.FindSourceFile(isSequential)
    WorkflowFileHelper.CreateBackupFile(vbFilePath)

    'overwrite the source file
    Dim wasReadOnly As Boolean = False
    Dim info As System.IO.FileInfo = New System.IO.FileInfo(vbFilePath)
    If info.Exists Then
      wasReadOnly = info.IsReadOnly
      info.IsReadOnly = False
      Dim tempFile As String = String.Format("{0}.temp", vbFilePath)
      Using objWriter As System.IO.StreamWriter = New System.IO.StreamWriter(tempFile, False)
        objWriter.Write(newVbText)
      End Using
      info.Delete()
      System.IO.File.Copy(tempFile, vbFilePath, False)
      System.IO.File.Delete(tempFile)
    End If

    'save settings
    SaveSettings(isSequential)

    'set the new loaded workflow
    ComposeWorkflowFile(True)

    'the source .vb file is changed: restart this program
    Application.Current.Shutdown(0)
  Catch ex As Exception
    OnError("Loading file", ex)
  End Try
End Sub

Private Sub WorkflowResetButton_Click(sender As Object, e As RoutedEventArgs) Handles WorkflowResetButton.Click
  Dim isSequential As Boolean = SequentialRadioButton.IsChecked
  Dim fileName As String = "SequentialProgram.vb"
  If Not isSequential Then
    fileName = "NavigatorProgram.vb"
  End If
  Dim message As String = String.Format("Do you really want to reset the workflow? The source file '{0}' will be cleared and your changes will be lost!", fileName)
  Dim result As Integer = MessageBox.Show(message, "Reset workflow", System.Windows.Forms.MessageBoxButtons.YesNo, MessageBoxImage.Exclamation)
  If result = System.Windows.Forms.DialogResult.Yes Then
    Try
      'create a backup of the SequentialProgram.vb or NavigatorProgram.vb source file
      Dim workflowHelper As WorkflowFileHelper = New WorkflowFileHelper(GetSourceFolderPath())
      Dim vbFilePath As String = workflowHelper.FindSourceFile(isSequential)
      WorkflowFileHelper.CreateBackupFile(vbFilePath)

      'generate the empty .vb file
      Dim newVbText As String = workflowHelper.ComposeSourceFile(isSequential, Nothing, Nothing, Nothing)

      'overwrite the source file
      Dim wasReadOnly As Boolean = False
      Dim info As System.IO.FileInfo = New System.IO.FileInfo(vbFilePath)
      If info.Exists Then
        wasReadOnly = info.IsReadOnly
        info.IsReadOnly = False
        Dim tempFile As String = String.Format("{0}.temp", vbFilePath)
        Using objWriter As System.IO.StreamWriter = New System.IO.StreamWriter(tempFile, False)
          objWriter.Write(newVbText)
        End Using
        info.Delete()
        System.IO.File.Copy(tempFile, vbFilePath, False)
        System.IO.File.Delete(tempFile)
      End If

      'clear the visual controls
      ResetTabs()

      CommentTextBox.Text = Nothing
      CommentRichTextBox.Document.Blocks.Clear()
      CommentRichTextBox.Document.LineHeight = 1
      ReferencesListView.Items.Clear()
      SetReferencesHeader()
      DescriptionTextBox.Text = Nothing
      DisplayProgressRadioButton.IsChecked = True
      TimeoutTextBox.Text = Nothing
      If isSequential Then
        TimeoutTextBox.IsEnabled = False
      Else
        TimeoutTextBox.IsEnabled = True
      End If
      SaveSettings(isSequential)

      'the source .vb file is changed: restart this program
      Application.Current.Shutdown(0)
    Catch ex As Exception
      OnError("Resetting workflow", ex)
    End Try
  End If
End Sub

Private Sub ReferencesAddButton_Click(sender As Object, e As RoutedEventArgs) Handles ReferencesAddButton.Click
  ReferencesErrorTextBox.Text = Nothing
  ReferencesErrorTextBox.Visibility = System.Windows.Visibility.Collapsed

  Dim assemblyStr As String = ReferencesTextBox.Text
  If String.IsNullOrEmpty(assemblyStr) Then
    ReferencesErrorTextBox.Text = "Dll name cannot be empty"
    ReferencesErrorTextBox.Visibility = System.Windows.Visibility.Visible
    Return
  End If
  Dim alreadyExists As Boolean = False
  If ReferencesListView.Items.Count > 0 Then
    For Each item As String In ReferencesListView.Items
      If String.Compare(item, assemblyStr, True) = 0 Then
        ReferencesListView.Items(ReferencesListView.Items.IndexOf(item)) = assemblyStr
        alreadyExists = True
        Exit For
      End If
    Next
  End If
  If Not alreadyExists Then
    ReferencesListView.Items.Add(assemblyStr)
  End If

  ReferencesListView.SelectedItem = Nothing
  ReferencesTextBox.Clear()
  SetReferencesHeader()
End Sub

Private Sub ReferencesRemoveButton_Click(sender As Object, e As RoutedEventArgs) Handles ReferencesRemoveButton.Click
  ReferencesErrorTextBox.Text = Nothing
  ReferencesErrorTextBox.Visibility = System.Windows.Visibility.Collapsed

  Dim selItem As String = Nothing
  If ReferencesListView.SelectedItems.Count > 0 Then
    selItem = ReferencesListView.SelectedItems(0)
  End If
  If selItem IsNot Nothing Then
    ReferencesListView.Items.Remove(selItem)
    ReferencesListView.SelectedItem = Nothing
  End If
  ReferencesTextBox.Clear()
  SetReferencesHeader()
End Sub

Private Sub ReferencesClearButton_Click(sender As Object, e As RoutedEventArgs) Handles ReferencesClearButton.Click
  ReferencesTextBox.Clear()
  ReferencesListView.Items.Clear()
  ReferencesListView.SelectedItem = Nothing
  SetReferencesHeader()
End Sub

Private Sub SetReferencesHeader()
  Dim referencesCount As String = Nothing
  If ReferencesListView.Items.Count > 0 Then
    referencesCount = String.Format(" ({0})", ReferencesListView.Items.Count)
  End If
  ReferencesGroupBox.Header = String.Format("References{0}", referencesCount)
End Sub

Private Sub TimeoutTextBox_LostFocus(sender As Object, e As RoutedEventArgs)
  TimeoutErrorTextBox.Visibility = System.Windows.Visibility.Collapsed
  If ApplicationSettings.IsSequential Then
    ApplicationSettings.SequentialData.Timeout = Nothing 'only for Navigator
  Else
    ApplicationSettings.NavigatorData.Timeout = Nothing
    If Not String.IsNullOrEmpty(TimeoutTextBox.Text) Then
      Try
        ApplicationSettings.NavigatorData.Timeout = TimeSpan.Parse(TimeoutTextBox.Text)
      Catch ex As Exception
        TimeoutErrorTextBox.Text = String.Format("Invalid timeout: {0}", ex.Message)
        TimeoutErrorTextBox.Visibility = System.Windows.Visibility.Visible
      End Try
    End If
  End If
End Sub

Private Sub CommentClearButton_Click(sender As Object, e As RoutedEventArgs) Handles CommentClearButton.Click
  CommentRichTextBox.Document.Blocks.Clear()
  CommentRichTextBox.Document.LineHeight = 1
End Sub

Private Sub SequentialRadioButton_Checked(sender As Object, e As RoutedEventArgs)
  If Me.IsInitialized Then
    'first save the Navigator last used data
    SaveSettings(False)
    'then display the Sequential data
    SetSettings(True)
  End If
End Sub

Private Sub NavigatorRadioButton_Checked(sender As Object, e As RoutedEventArgs)
  If Me.IsInitialized Then
    'first save the Sequential last used data
    SaveSettings(True)
    'then display the Navigator data
    SetSettings(False)
  End If
End Sub

Private Sub OnError(descr As String, ex As Exception)
  ResetTabs()
  If ex Is Nothing Then
    Return 'nothing to do
  End If

  Dim testProgress As TesterProgress = New TesterProgress(ProgressRichTextBox)
  testProgress.WriteLine()
  testProgress.WriteLineColor(Brushes.Red, FontWeights.Bold, "{0}. '{1}'", descr, Utility.GetInternalException(ex).Message)

  MainTabControl.SelectedItem = ResultTabItem
  ProgressTabControl.SelectedItem = ProgressTabItem
  MainTabControl.UpdateLayout()
End Sub

Private Sub ResetTabs()
  'reset the progress tab
  ProgressRichTextBox.Document.Blocks.Clear()
  ProgressRichTextBox.Document.LineHeight = 1
  OutputRichTextBox.Document.Blocks.Clear()
  OutputRichTextBox.Document.LineHeight = 1
  'remove the workflow messages
  ResultTextBox.Clear()
  'remove the workflow logs
  LogsGrid.Visibility = System.Windows.Visibility.Collapsed
  For idx As Integer = (LogsGrid.RowDefinitions.Count - 1) To 0 Step -1
    LogsGrid.RowDefinitions.RemoveAt(idx)
  Next
End Sub

''' <summary>Gets the global result.
''' The global result is the maximum code of all the log entry (if an error occurred we assume
''' that the whole WF is in error)</summary>
''' <param name="result">The workflow result</param>
''' <returns>The maximum code of all the log entry</returns>
Private Shared Function GetGlobalResult(wfResult As Workflow.Result) As Integer
  If wfResult Is Nothing Then
    Throw New ArgumentNullException("wfResult")
  End If

  Dim retValue As Integer = 0 '0 = info
  If wfResult.Log IsNot Nothing Then
    For Each entry As LogEntry In wfResult.Log
      If entry.Code > retValue Then
        retValue = entry.Code
        If retValue = 2 Then '2 = error
          Exit For
        End If
      End If
    Next
  End If
  Return retValue
End Function

''' <summary>Return the result message of the workflow execution</summary>
''' <param name="result">The workflow result</param>
''' <returns>The message to display</returns>
Private Function GetGeneralMessage(wfResult As Workflow.Result) As String
  If wfResult Is Nothing Then
    Throw New ArgumentNullException("wfResult")
  End If
  Dim code As Integer = GetGlobalResult(wfResult)

  Dim description As String = wfResult.Description
  If String.IsNullOrEmpty(description) Then
    description = FileNameTextBox.Text
  End If

  Select Case code
    Case 0 'info
      Return description & " completed successfully"
    Case 1 'warning
      Return description & " completed with warning(s)"
    Case 2 'error
      Return description & " completed with error(s)"
    Case Else 'valid for code = -1 (Unknown)
      Return description & " completed successfully"
  End Select
End Function

''' <summary>Show the complete workflow result</summary>
''' <param name="result">The workflow result</param>
Private Sub ShowWorkflowResult(wfResult As Workflow.Result)
  LogsGrid.Visibility = System.Windows.Visibility.Visible
  If wfResult Is Nothing Then
    Return 'nothing to do
  End If

  'general workflow report
  Dim sTmp As String = GetGeneralMessage(wfResult)
  If Not String.IsNullOrEmpty(ResultMessage) Then
    sTmp &= ResultMessage
  End If

  Dim eTmp As Workflow.LogEntry = New Workflow.LogEntry(Workflow.LogEntry.LogCode.Unknown, Workflow.LogEntry.LogFormat.Text, sTmp)
  AppendLog(eTmp)

  If wfResult.Log IsNot Nothing AndAlso wfResult.Log.Count > 0 Then
    For Each entry As Workflow.LogEntry In wfResult.Log
      AppendLog(entry)
    Next
  End If
End Sub

Private Sub AppendLog(entry As Workflow.LogEntry)
  If entry Is Nothing Then
    Throw New ArgumentNullException("entry")
  End If

  Dim rowIndex As Integer = LogsGrid.RowDefinitions.Count
  Dim row As RowDefinition = New RowDefinition()
  row.Height = GridLength.Auto
  LogsGrid.RowDefinitions.Add(row)

  Dim icon As Image = New Image()
  icon.Width = 16
  icon.Height = 16
  If entry.Code >= 0 And entry.Code <= 2 Then
    icon.Source = LogIcons(entry.Code)
  End If
  Grid.SetColumn(icon, 0)
  LogsGrid.Children.Add(icon)
  Grid.SetRow(icon, rowIndex)

  Dim logTextBox As TextBox = New TextBox()
  logTextBox.TextWrapping = TextWrapping.Wrap
  logTextBox.Text = entry.Message.TrimStart(Environment.NewLine)
  logTextBox.Background = Brushes.Transparent
  logTextBox.BorderThickness = New Thickness(0)
  logTextBox.IsReadOnly = True
  logTextBox.Height = Double.NaN 'auto height

  Grid.SetColumn(logTextBox, 1)
  LogsGrid.Children.Add(logTextBox)
  Grid.SetRow(logTextBox, rowIndex)
End Sub

''' <summary>Check if there is at least of log with 'error' code</summary>
''' <param name="login">The current login</param>
''' <param name="wfResult">The workflow result</param>
''' <returns>True if there is an error; false otherwise</returns>
Public Shared Function WriteResult(login As ServerLogin, wfResult As Workflow.Result) As Boolean
  WorkflowResult = wfResult
  If wfResult Is Nothing Then
    ResultMessage = Nothing
    Return False 'nothing to do
  End If
  ResultMessage = ResultBase.ResultsToMessage(login, wfResult.ImportResults, Nothing, Nothing)
  Return (GetGlobalResult(wfResult) = 2) '2=error
End Function

Private Async Function RunWorkflowAsync(
  sequential As Boolean,
  ids() As IdVersion,
  parameters As System.Collections.Specialized.NameValueCollection,
  files() As String) As Task(Of Boolean)

  WorkflowResult = Nothing
  ResultMessage = Nothing
  Dim hasLogError As Boolean = False
  Dim testProgress As TesterProgress = New TesterProgress(ProgressRichTextBox)
  ResetTabs()
  MainTabControl.SelectedItem = ResultTabItem
  ProgressTabControl.SelectedItem = ProgressTabItem
  MainTabControl.UpdateLayout()

  'pass the .eml file path as parameter to the wf test manager (to not change the methods interface)
  If Not String.IsNullOrEmpty(EmailTextBox.Text) Then
    If parameters Is Nothing Then
      parameters = New System.Collections.Specialized.NameValueCollection()
    End If
    parameters.Add(Server.WFTest.TestEmailFileParameter, EmailTextBox.Text)
  End If

  Try
    If sequential Then
      Await Task.Run(
        Sub()
          hasLogError = RunSequential(ids, parameters, files, testProgress)
        End Sub
      )
    Else
      hasLogError = RunNavigator(ids, parameters, files, testProgress)
    End If

    'show workflow logs
    ShowWorkflowResult(WorkflowResult)

    testProgress.WriteLine()
    If hasLogError Then
      testProgress.WriteLineColor(Brushes.Red, FontWeights.Bold, "Workflow execution complete with error")
    Else
      testProgress.WriteLineColor(Brushes.Green, FontWeights.Bold, "Workflow execution complete successfully")
    End If
  Catch ex As Exception
    testProgress.WriteLine()
    testProgress.WriteLineColor(Brushes.Red, FontWeights.Bold, "Workflow execution failed with error '{0}'", ex.Message)
  Finally
    ExecuteButton.IsEnabled = True
    ExecuteButton.Content = "Execute"
    OutputRichTextBox.ScrollToEnd()
    ProgressRichTextBox.ScrollToEnd()
  End Try

  Return hasLogError
End Function

Private Function RunSequential(
  ids() As IdVersion,
  parameters As System.Collections.Specialized.NameValueCollection,
  files() As String,
  testProgress As TesterProgress) As Boolean

  Dim executionLog As XElement = Nothing

  If testProgress IsNot Nothing Then
    testProgress.WriteLine("Starting sequential workflow..." & vbNewLine)
  End If

  'sequential workflow test call
  Return WFTest.RunSequential(
    GetType(SequentialWorkflow),
    ids,
    WFTest.DataFiles(files),
    parameters,
    executionLog,
    New TextBoxWriter(OutputRichTextBox),
    testProgress,
    writeResultMethod)
End Function

Private Function RunNavigator(
  ids() As IdVersion,
  parameters As System.Collections.Specialized.NameValueCollection,
  files() As String,
  testProgress As TesterProgress) As Boolean

  Dim executionLog As XElement = Nothing
  Dim gn4Url As String = Utility.EnsureLastSlash(SettingsGN4UrlTextBox.Text)
  If String.IsNullOrEmpty(gn4Url) Then
    Throw New Exception("Please, set the 'GN4 Url' parameter in Settings tab")
  End If
  'userName and password can be empty if working with Windows Authentication
  Dim userName As String = SettingsUserNameTextBox.Text
  Dim password As String = SettingsPasswordTextBox.Text

  If testProgress IsNot Nothing Then
    testProgress.WriteLine("Starting navigator workflow... (see the workflow panel)" & vbNewLine)
  End If

  ' Navigator workflow test call
  ' we have to specify server url and user credentials to emulate a client login
  Return WFTest.RunNavigator(
    GetType(NavigatorWorkflow),
    ids,
    WFTest.DataFiles(files),
    parameters,
    executionLog,
    gn4Url,
    userName,
    password,
    New TextBoxWriter(OutputRichTextBox),
    testProgress,
    writeResultMethod)
End Function

End Class
