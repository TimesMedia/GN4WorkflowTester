Miles33/Tera DP

Workflow Tester application

--------------------

To use the WorkflowTester application you have to:
1) copy into the .\bin folder the GN4 dlls.
   You can get the GN4bin_64.zip from 
     http://tech.teradp.com/tech/download/gnportal/rel22/GN4bin_64.zip
   and expand it into the .\bin folder
2) configure the access to the database in the appSettings.xml file
3) open the WorkflowTesterExpress solution using VisualStudio and run the application

The documentation about the WorkflowTester.exe application is the CodeWorkflow.docx file.

--------------------

Note that:
- WorkflowTester can run in 2.2 and Main (from the revision 2.2.2621)
- when you load a workfow from file or database, the program automatically changes the .vb source file (SequentialProgram.vb or NavigatorProgram.vb) and then it shutdowns.
  At restarting, the new workflow will be recompiled and loaded.

--------------------

