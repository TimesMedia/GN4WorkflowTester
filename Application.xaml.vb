Imports TeraDP.GN4.WinUI

Class WorkflowTesterApplication : Inherits ApplicationBase

  ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
  ' can be handled in this file.

  Overrides ReadOnly Property ApplicationName As String
    Get
      Return "WFTest"
    End Get
  End Property

End Class
