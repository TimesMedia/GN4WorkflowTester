md exportSOSBX
srv4 export -opt exportopt.xml -conditions gn4:justScope[@name='SO'] -out .\exportSOSBX\1_justScope.xml
srv4 export -opt exportopt.xml -conditions gn4:title[@name='SO'] -out .\exportSOSBX\2_title.xml
srv4 export -opt exportopt.xml -conditions gn4:section[gn4:titleRef/nav:refObject/gn4:title/@name='SO'] -out .\exportSOSBX\3_sections.xml
srv4 export -opt exportopt.xml -conditions gn4:editionNumber[gn4:titleRef/nav:refObject/gn4:title/@name='SO'] -out .\exportSOSBX\4_editionNumber.xml
srv4 export -opt exportopt.xml -conditions gn4:region[gn4:titleRef/nav:refObject/gn4:title/@name='SO'] -out .\exportSOSBX\5_region.xml
srv4 export -opt exportopt.xml -conditions gn4:zone[gn4:titleRef/nav:refObject/gn4:title/@name='SO'] -out .\exportSOSBX\6_zone.xml
srv4 export -opt exportopt.xml -conditions gn4:book[gn4:titleRef/nav:refObject/gn4:title/@name='SO'] -out .\exportSOSBX\7_book.xml
srv4 export -opt exportopt.xml -conditions gn4:copyright[gn4:titleRef/nav:refObject/gn4:title/@name='SO'] -out .\exportSOSBX\8_copyright.xml
srv4 export -opt exportopt.xml -conditions gn4:hyphenation[gn4:scopeRef/nav:refObject/gn4:justScope/@name='SO'] -out .\exportSOSBX\9_hyphenation.xml
srv4 export -opt exportopt.xml -conditions gn4:fontLayout[gn4:scopeRef/nav:refObject/gn4:justScope/@name='SO'] -out .\exportSOSBX\10a_fontLayout.xml
srv4 export -opt exportopt.xml -conditions gn4:font[gn4:scopeRef/nav:refObject/gn4:justScope/@name='SO'] -out .\exportSOSBX\10b_fonts.xml
srv4 export -opt exportopt.xml -conditions gn4:justDef[gn4:scopeRef/nav:refObject/gn4:justScope/@name='SO'] -out .\exportSOSBX\11_justDef.xml
srv4 export -opt exportopt.xml -conditions gn4:justContext[gn4:scopeRef/nav:refObject/gn4:justScope/@name='SO'] -out .\exportSOSBX\12_justContext.xml
srv4 export -opt exportopt.xml -conditions gn4:geometry[gn4:scopeRef/nav:refObject/gn4:justScope/@name='SO'] -out .\exportSOSBX\13_geometry.xml
srv4 export -opt exportopt.xml -conditions gn4:folder[starts-with(@path,'/SO')] -out .\exportSOSBX\14_folders.xml
