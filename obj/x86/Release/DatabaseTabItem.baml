   M S B A M L   `   `   `  ����   WorkflowTester)    #TeraDP.GN4.Workflow.DatabaseTabItem   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     @d2http://schemas.microsoft.com/expression/blend/2008     .���    	GridStyle#��[  $��6,6,6,6q�  � RowDefinitions  �� $
��Auto ��� $��* ��� $
��Auto �.+��� -    ��Name$ DatabaseErrorTextBox��$��0�� �� TextWrapping$
 Wrap=�
���  ��$��0q�
������ $
��Bold"� �� 
IsReadOnly . $
��Auto��$y�	Collapsed=����� *Text+ �� Mode$ OneWay=��� -    ��Name$ DatabaseListView��$��1��$y�	Collapsed=� �� View �� .P��� $��	Workflows�� �� Width$	 200��� -    �Name$ DatabaseButtonGrid��$��2��$��0,6,0,0q�	 � ColumnDefinitions	 �� $��* ��� $
��Auto ��� $
��Auto ��� $
��Auto �.+��� -   
 ��Name$
 DatabaseNewButton��$��1��$��
Create New��{�.   BigButtonStyle#��[ �� -   $
 DatabaseSaveButton��$��2��$��	Overwrite��{�.  #��[ �� -   $
 DatabaseLoadButton��$��3��$
��Load��{�.  #��[ 