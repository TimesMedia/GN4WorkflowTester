Imports TeraDP.GN4.Server
Imports TeraDP.GN4.Server.CodeActivity

Public Class DatabaseTabItem : Inherits TabItem
#Region "Members"

  Dim Context As TestWorkflowContext = Nothing
  Dim IsDataLoaded As Boolean = False
  Private CustomWindow As Window = Nothing
  Private NewWorkflowName As String = Nothing
  Private MessageToShow As String = Nothing

#End Region
#Region "Operations"

  Public Function Initialize() As String
    Dim errorMessage As String = Nothing
    If Context IsNot Nothing Then Return errorMessage
    Context = Server.TestWorkflowContext.GetTestContext(errorMessage)
    Return errorMessage
  End Function

  Public Async Function LoadDataAsync() As Task
    If IsDataLoaded Then Return
    OnInfo("Loading data...")
    Await Task.Run(Sub()
                     LoadData()
                   End Sub)
  End Function

  Private Sub LoadData()
    If IsDataLoaded Then Return
    UpdateListViewSafe(System.Windows.Visibility.Collapsed)

    Try
      Dim message As String = Initialize()
      If Not String.IsNullOrEmpty(message) Then
        OnError(message)
        Return
      End If

      'look for all the workflows into the database
      Dim searchAct As Search = New Search(Context) With {.Name = "list workflows"}
      searchAct.XQuery = "gn4:config[starts-with(@name,'wf_')]"
      Dim searchRes As SearchResult = searchAct.Do()
      If searchRes.IdsCount = 0 Then
        OnError("No workflow found.")
        Return
      End If

      'load the workflow names
      Dim loadWorkflowAct As LoadObjects = New LoadObjects(Context) With {.Name = "load workflow names"}
      loadWorkflowAct.Xslt =
<xsl:stylesheet
version="1.0"
xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:fn="http://www.teradp.com/schemas/GN4/1/Xslt"
xmlns:gn4="urn:schemas-teradp-com:gn4tera">
  <xsl:template match="gn4:config">
    <workflow id="{fn:objectIdFromString(@id)}" name="{@name}"/>
  </xsl:template>
  <xsl:template match="/*">
    <workflows>
      <xsl:apply-templates/>
    </workflows>
  </xsl:template>
</xsl:stylesheet>
      loadWorkflowAct.ObjectIds = searchRes.IdsOut
      Dim loadWorkflowRes As LoadObjectsResult = loadWorkflowAct.Do()

      'fill in the listview
      FillInListViewSafe(loadWorkflowRes.XmlOut)

      isDataLoaded = True
      OnInfo(MessageToShow)
      MessageToShow = nothing
    Catch ex As Exception
      isDataLoaded = False
      OnError("Loading workflow data", ex)
    End Try
  End Sub

  Private Sub OnError(descr As String, ex As Exception)
    Dim message As String = Nothing
    If ex IsNot Nothing Then
      message = String.Format("{0}. '{1}'", descr, Utility.GetInternalException(ex).Message)
    End If
    OnError(message)
  End Sub

  Private Sub OnInfo(message As String)
    UpdateErrorTextBoxSafe(message, False)
  End Sub

  Private Sub OnError(message As String)
    UpdateErrorTextBoxSafe(message, True)
  End Sub

  Private Function CreateConfirmationDialog(workflowName As String) As Window
    Const margin As Integer = 4

    Dim askWindow As Window = New Window()
    askWindow.WindowStyle = WindowStyle.ToolWindow
    askWindow.Background = SystemColors.ControlBrush
    askWindow.Owner = WorkflowFileHelper.MainWnd
    askWindow.Title = "Create new workflow"
    askWindow.Width = 360
    askWindow.Height = 200
    'askWindow.Icon = CType(WorkflowFileHelper.MainWnd.TryFindResource("gn4_icon"), ImageSource)
    NameScope.SetNameScope(askWindow, New NameScope()) 'to use FindName()

    Dim mainGrid As Grid = New Grid()
    'mainGrid.ShowGridLines = True
    askWindow.Content = mainGrid

    '3 columns (1 filler)
    Dim labelColumn As ColumnDefinition = New ColumnDefinition()
    Dim fillerColumn As ColumnDefinition = New ColumnDefinition()
    Dim textColumn As ColumnDefinition = New ColumnDefinition()
    labelColumn.Width = GridLength.Auto
    fillerColumn.Width = New GridLength(1, GridUnitType.Star)
    textColumn.Width = GridLength.Auto
    mainGrid.ColumnDefinitions.Add(labelColumn)
    mainGrid.ColumnDefinitions.Add(fillerColumn)
    mainGrid.ColumnDefinitions.Add(textColumn)

    '4 rows (1 filler)
    Dim messageRow As RowDefinition = New RowDefinition()
    Dim textRow As RowDefinition = New RowDefinition()
    Dim fillerRow As RowDefinition = New RowDefinition()
    Dim buttonRow As RowDefinition = New RowDefinition()
    messageRow.Height = GridLength.Auto
    textRow.Height = GridLength.Auto
    fillerRow.Height = New GridLength(1, GridUnitType.Star)
    buttonRow.Height = GridLength.Auto
    mainGrid.RowDefinitions.Add(messageRow)
    mainGrid.RowDefinitions.Add(textRow)
    mainGrid.RowDefinitions.Add(fillerRow)
    mainGrid.RowDefinitions.Add(buttonRow)

    Dim msgLabel As Label = New Label()
    msgLabel.Content = "Creating a new workflow into the database."
    Utility.SetMargins(msgLabel, margin, margin, 8, margin)
    Grid.SetColumn(msgLabel, 0)
    Grid.SetRow(msgLabel, 0)
    Grid.SetColumnSpan(msgLabel, 3)
    mainGrid.Children.Add(msgLabel)

    Dim nameLabel As Label = New Label()
    nameLabel.Content = "Workflow name:"
    Utility.SetMargins(nameLabel, margin, margin, margin, margin)
    Grid.SetColumn(nameLabel, 0)
    Grid.SetRow(nameLabel, 1)
    mainGrid.Children.Add(nameLabel)

    Dim msgTextBox As TextBox = New TextBox()
    msgTextBox.Text = workflowName
    msgTextBox.MinWidth = 200
    msgTextBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
    Utility.SetMargins(msgTextBox, margin, margin, margin, margin)
    Grid.SetColumn(msgTextBox, 1)
    Grid.SetRow(msgTextBox, 1)
    Grid.SetColumnSpan(msgTextBox, 2)
    mainGrid.Children.Add(msgTextBox)
    askWindow.RegisterName("NameTextBox", msgTextBox)

    Dim buttonGrid As Grid = New Grid()
    buttonGrid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
    Grid.SetColumn(buttonGrid, 0)
    Grid.SetRow(buttonGrid, 3)
    Grid.SetColumnSpan(buttonGrid, 3)

    '3 columns (1 filler)
    Dim fillerButtonColumn As ColumnDefinition = New ColumnDefinition()
    Dim secondButtonColumn As ColumnDefinition = New ColumnDefinition()
    Dim thirdButtonColumn As ColumnDefinition = New ColumnDefinition()
    fillerButtonColumn.Width = New GridLength(1, GridUnitType.Star)
    secondButtonColumn.Width = GridLength.Auto
    thirdButtonColumn.Width = GridLength.Auto
    buttonGrid.ColumnDefinitions.Add(fillerButtonColumn)
    buttonGrid.ColumnDefinitions.Add(secondButtonColumn)
    buttonGrid.ColumnDefinitions.Add(thirdButtonColumn)

    Dim yesButton As Button = New Button()
    yesButton.Content = "Yes"
    yesButton.Width = 60
    yesButton.Height = 22
    yesButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right
    Utility.SetMargins(yesButton, margin, margin, margin, margin)
    Grid.SetColumn(yesButton, 1)
    buttonGrid.Children.Add(yesButton)
    AddHandler yesButton.Click, AddressOf YesButton_Click

    Dim noButton As Button = New Button()
    noButton.Content = "No"
    noButton.Width = 60
    noButton.Height = 22
    noButton.HorizontalAlignment = System.Windows.HorizontalAlignment.Right
    noButton.IsCancel = true
    noButton.IsDefault = true
    Utility.SetMargins(noButton, margin, margin, margin, margin)
    Grid.SetColumn(noButton, 2)
    buttonGrid.Children.Add(noButton)
    AddHandler noButton.Click, AddressOf NoButton_Click

    mainGrid.Children.Add(buttonGrid)
    Return askWindow
  End Function

  Private Sub EnableButtons(show As Boolean)
    DatabaseNewButton.IsEnabled = show
    DatabaseLoadButton.IsEnabled = show
    DatabaseSaveButton.IsEnabled = show
  End Sub

#End Region
#Region "Events"

  Private Sub YesButton_Click(sender As Object, e As RoutedEventArgs)
    If CustomWindow IsNot Nothing Then
      Dim tBox As TextBox = CustomWindow.FindName("NameTextBox")
      If tBox IsNot Nothing Then
        NewWorkflowName = tBox.Text
      End If
      CustomWindow.DialogResult = True
      CustomWindow.Close()
      CustomWindow = Nothing
    End If
  End Sub

  Private Sub NoButton_Click(sender As Object, e As RoutedEventArgs)
    If customWindow IsNot Nothing Then
      customWindow.DialogResult = False
      customWindow.Close()
      CustomWindow = Nothing
      NewWorkflowName = Nothing
    End If
  End Sub

  Private Sub DatabaseListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
    Dim selItem As ListViewItem = Nothing
    If DatabaseListView.SelectedItems.Count > 0 Then
      selItem = DatabaseListView.SelectedItems(0)
    End If
    Dim buttonEnabled As Boolean = selItem IsNot Nothing
    DatabaseLoadButton.IsEnabled = buttonEnabled
    DatabaseSaveButton.IsEnabled = buttonEnabled
  End Sub

  Private Sub DatabaseNewButton_Click(sender As Object, e As RoutedEventArgs) Handles DatabaseNewButton.Click
    OnError(Nothing)
    EnableButtons(False)
    DatabaseListView.SelectedItem = Nothing

    Try
      Dim configId As Integer = 0
      Dim workflowName As String = WorkflowFileHelper.GetWFFileName(WorkflowFileHelper.MainWnd.FileNameTextBox.Text)

      'ask confirmation
      customWindow = CreateConfirmationDialog(workflowName)
      Dim okSave As Boolean? = customWindow.ShowDialog()
      If Not okSave = True Then
        Return
      End If
      If String.IsNullOrEmpty(NewWorkflowName) Then
        OnError("The workflow cannot have an empty name.")
        Return
      End If
      'remove the .xml extension (if needed)
      If NewWorkflowName.EndsWith(".xml") Then
        NewWorkflowName = NewWorkflowName.Substring(0, NewWorkflowName.Length - 4)
      End If

      'check if the name already exists
      Dim searchNameAct As Search = New Search(Context) With {.Name = "check workflow name"}
      searchNameAct.XQuery = String.Format("gn4:config[@name='{0}']", NewWorkflowName)
      Dim searchNameRes As SearchResult = searchNameAct.Do()
      If searchNameRes.IdsCount > 0 Then
        OnError(String.Format("There is already a workflow having the name '{0}' (id: {1}).", NewWorkflowName, searchNameRes.IdsOut(0)))
        Return
      End If
      WorkflowFileHelper.MainWnd.FileNameTextBox.Text = NewWorkflowName

      Dim wfString As String = _
        New TextRange(WorkflowFileHelper.MainWnd.WorkflowRichTextBox.Document.ContentStart, WorkflowFileHelper.MainWnd.WorkflowRichTextBox.Document.ContentEnd).Text
      If String.IsNullOrEmpty(wfString) Then
        OnError("Cannot read the new workflow data")
        Return
      End If

      'check if valid xml  
      Dim xWFDocument As XDocument = nothing
      Try
        xWFDocument = XDocument.Parse(wfString, LoadOptions.PreserveWhitespace)
      Catch ex As Exception
        OnError(String.Format("Invalid workflow xml: {0}", ex.Message))
        Return
      End Try

      If xWFDocument IsNot Nothing Then
        'save into the database
        Dim wfId As Integer = Context.PutConfig(NewWorkflowName, xWFDocument.GetXmlDocument()) 
        'reload the workflows from the database
        isDataLoaded = False
        MessageToShow = String.Format("Created the workflow '{0}' (id: {1}).", NewWorkflowName, wfId) 
        LoadDataAsync()
      End If
    Catch ex As Exception
      OnError("Creating workflow", ex)
    Finally
      NewWorkflowName = Nothing
      DatabaseNewButton.IsEnabled = True
      Dim show As Boolean = DatabaseListView.SelectedItems.Count > 0
      DatabaseLoadButton.IsEnabled = show
      DatabaseSaveButton.IsEnabled = show
    End Try
  End Sub

  Private Sub DatabaseLoadButton_Click(sender As Object, e As RoutedEventArgs) Handles DatabaseLoadButton.Click
    OnError(Nothing)
    EnableButtons(False)

    Try
      Dim configId As Integer = 0
      Dim workflowName As String = Nothing
      Dim selItem As ListViewItem = Nothing

      If DatabaseListView.SelectedItems.Count > 0 Then
        selItem = DatabaseListView.SelectedItems(0)
      End If
      If selItem Is Nothing Then Return

      configId = selItem.Tag
      If configId = 0 Then Return
      workflowName = selItem.Content

      'load the workflow from database
      Dim loadWorkfowDataAct As LoadData = New LoadData(Context) With {.Name = "load workflow data"}
      loadWorkfowDataAct.ObjectIds.Add(configId)
      loadWorkfowDataAct.AttributeName = "data"
      Dim loadWorkfowDataRes As LoadDataResult = loadWorkfowDataAct.Do()
      If loadWorkfowDataRes.DataOut.Count = 0 Then
        OnError(String.Format("Cannot load the workflow data ({0}): {1}", configId, workflowName))
        Return
      End If

      'read the xml of the workflow
      Dim loadWorkflowXmlAct As LoadXml = New LoadXml(Context) With {.Name = "load workflow xml"}
      loadWorkflowXmlAct.Data = loadWorkfowDataRes.DataOut(0)
      Dim loadWorkflowXmlRes As LoadXmlResult = loadWorkflowXmlAct.Do()

      'set the workflow data and close
      WorkflowFileHelper.MainWnd.LoadWorkflow(workflowName, loadWorkflowXmlRes.XmlOut)
    Catch ex As Exception
      OnError("Loading workflow", ex)
    Finally
      EnableButtons(True)
    End Try
  End Sub

  Private Sub DatabaseSaveButton_Click(sender As Object, e As RoutedEventArgs) Handles DatabaseSaveButton.Click
    OnError(Nothing)
    EnableButtons(False)

    Try
      Dim configId As Integer = 0
      Dim workflowName As String = Nothing
      Dim selItem As ListViewItem = Nothing

      If DatabaseListView.SelectedItems.Count > 0 Then
        selItem = DatabaseListView.SelectedItems(0)
      End If
      If selItem Is Nothing Then Return

      configId = selItem.Tag
      If configId = 0 Then Return
      workflowName = selItem.Content

      'ask confirmation
      Dim result As MsgBoxResult = MessageBox.Show(String.Format("Overwriting the '{0}' workflow into the database (id: {1}). Are you sure?", workflowName, configId), _
        "Save workflow", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
      If Not result = MsgBoxResult.Yes Then Return

      'load the workflow from database
      Dim loadWorkflowDataAct As LoadData = New LoadData(Context) With {.Name = "load workflow data"}
      loadWorkflowDataAct.ObjectIds.Add(configId)
      loadWorkflowDataAct.AttributeName = "data"
      Dim loadWorkflowDataRes As LoadDataResult = loadWorkflowDataAct.Do()
      If loadWorkflowDataRes.DataOut.Count = 0 Then
        OnError(String.Format("Cannot load the workflow data ({0}): {1}", configId, workflowName))
        Return
      End If

      'read the xml of the workflow to overwrite
      Dim loadWorkflowXmlAct As LoadXml = New LoadXml(Context) With {.Name = "load workflow xml"}
      loadWorkflowXmlAct.Data = loadWorkflowDataRes.DataOut(0)
      Dim loadWorkflowXmlRes As LoadXmlResult = loadWorkflowXmlAct.Do()

      'save a backup
      WorkflowFileHelper.CreateBackupFile(workflowName, loadWorkflowXmlRes.XmlOut)

      'read the new workflow xml 
      Dim wfString As String = _
        New TextRange(WorkflowFileHelper.MainWnd.WorkflowRichTextBox.Document.ContentStart, WorkflowFileHelper.MainWnd.WorkflowRichTextBox.Document.ContentEnd).Text
      If String.IsNullOrEmpty(wfString) Then
        OnError("Cannot read the new workflow data")
        Return
      End If

      'check if valid xml  
      Dim xWFDocument As XDocument = nothing
      Try
        xWFDocument = XDocument.Parse(wfString, LoadOptions.PreserveWhitespace)
      Catch ex As Exception
        OnError(String.Format("Invalid workflow xml: {0}", ex.Message))
        Return
      End Try

      If xWFDocument IsNot Nothing Then
        'save the workflow data into the database
        If loadWorkflowXmlRes.XmlOut IsNot Nothing Then
          Dim wfId As Integer = Context.PutConfig(workflowName, xWFDocument.GetXmlDocument())
          WorkflowFileHelper.MainWnd.FileNameTextBox.Text = workflowName
          OnInfo(String.Format("Saved the workflow '{0}' (id: {1}).", workflowName, wfId))
        End If
        DatabaseListView.SelectedItem = Nothing
      End if
    Catch ex As Exception
      OnError("Saving workflow", ex)
    Finally
      EnableButtons(True)
    End Try
  End Sub

#End Region
#Region "Different-thread safe methods"

  Delegate Sub UpdateErrorTextBoxDelegate(text As String, visibility As System.Windows.Visibility, color As System.Windows.Media.SolidColorBrush)
  Delegate Sub UpdateListViewDelegate(visibility As System.Windows.Visibility)
  Delegate Sub FillInListViewDelegate(xmlData As XDocument)

  Private Sub UpdateErrorTextBoxSafe(text As String, isError As Boolean)
    Dim visibility As System.Windows.Visibility = _
      If(String.IsNullOrEmpty(text), System.Windows.Visibility.Collapsed, System.Windows.Visibility.Visible)
    Dim color As System.Windows.Media.SolidColorBrush = _
      If(isError, System.Windows.Media.Brushes.Red, System.Windows.Media.Brushes.Black)

    If DatabaseErrorTextBox.CheckAccess() Then
      UpdateErrorTextBox(text, visibility, color)
    Else
      DatabaseErrorTextBox.Dispatcher.BeginInvoke(New UpdateErrorTextBoxDelegate(AddressOf UpdateErrorTextBox), text, visibility, color)
    End If
  End Sub

  Private Sub UpdateListViewSafe(visibility As System.Windows.Visibility)
    If DatabaseListView.CheckAccess() Then
      UpdateListView(visibility)
    Else
      DatabaseListView.Dispatcher.BeginInvoke(New UpdateListViewDelegate(AddressOf UpdateListView), visibility)
    End If
  End Sub

  Private Sub FillInListViewSafe(xmlData As XDocument)
    If DatabaseListView.CheckAccess() Then
      FillInListView(xmlData)
    Else
      DatabaseListView.Dispatcher.BeginInvoke(New FillInListViewDelegate(AddressOf FillInListView), xmlData)
    End If
  End Sub

  Private Sub UpdateErrorTextBox(text As String, visibility As System.Windows.Visibility, color As System.Windows.Media.SolidColorBrush)
    DatabaseErrorTextBox.Text = text
    DatabaseErrorTextBox.Visibility = visibility
    DatabaseErrorTextBox.Foreground = color
  End Sub

  Private Sub UpdateListView(visibility As System.Windows.Visibility)
    DatabaseListView.Visibility = visibility
    EnableButtons(visibility = System.Windows.Visibility.Visible)
  End Sub

  Private Sub FillInListView(xmlData As XDocument)
    If xmlData Is Nothing OrElse xmlData.Root Is Nothing Then
      Return
    End If

    Dim items As List(Of ListViewItem) = New List(Of ListViewItem)
    For Each xWorkflow As XElement In xmlData.Root.<workflow>
      Dim id As Integer = CInt(xWorkflow.@id)
      Dim newItem As ListViewItem = New ListViewItem()
      newItem.Tag = id
      newItem.Content = xWorkflow.@name
      items.Add(newItem)
    Next
    items.Sort(New SortComparer(True))

    DatabaseListView.ItemsSource = items
    DatabaseNewButton.IsEnabled = True
    DatabaseListView.Visibility = System.Windows.Visibility.Visible
  End Sub

#End Region
End Class

Public Class SortComparer
  Implements System.Collections.Generic.IComparer(Of ListViewItem)
  Private ascending As Boolean

  Public Sub New(ByVal asc As Boolean)
     Me.ascending = asc
  End Sub

  Public Function [Compare](ByVal x As ListViewItem, ByVal y As ListViewItem) As Integer Implements System.Collections.Generic.IComparer(Of ListViewItem).Compare
    Dim xText As String = x.Content
    Dim yText As String = y.Content
    Return xText.CompareTo(yText) * IIf(Me.ascending, 1, -1)
  End Function
End Class
