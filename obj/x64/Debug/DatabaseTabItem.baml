   M S B A M L   `   `   `  ����  WorkflowTester)    #TeraDP.GN4.Workflow.DatabaseTabItem   5   
   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      .��5      �    	GridStyle#��[  6	   $��6,6,6,6q�6,     � RowDefinitions  5	      �� 5
      $
��Auto �6   �� 5      $��* �6   �� 5      $
��Auto �6   .+�5      �� -   5         ��Name$ DatabaseErrorTextBox��5      $��0��6,    �� TextWrapping$
 Wrap=�69   
���  ��$��0q�6^   
������ $
��Bold"�5       �� 
IsReadOnly . $
��Auto��6N   $y�	Collapsed=�6\   ��6   �� *Text+ �� Mode$ OneWay=�6\   �� 5      -   5         ��Name$ DatabaseListView��5      $��1��6)   $y�	Collapsed=�66    �� View 5      �� 5   
   .P�5      �� $��	Workflows��6    �� Width$	 200��6.   5      5      � 5      -   5         �Name$ DatabaseButtonGrid��5      $��2��6'   $��0,6,0,0q�64   	 � ColumnDefinitions	 5      �� 5   
   $��* �6   �� 5   
   $
��Auto �6   �� 5   
   $
��Auto �6   �� 5   
   $
��Auto �6   .+�5      �� -   5        
 ��Name$
 DatabaseNewButton��5      $��1��6*   $��
Create New��6:   {�.   BigButtonStyle#��[ 6`   �� 5      -   5        $
 DatabaseSaveButton��5      $��2��6+   $��	Overwrite��6;   {�.  #��[ 6a   �� 5       -   5        $
 DatabaseLoadButton��5       $��3��6+   $
��Load��6;   {�.  #��[ 6\   5!      5"      5#      