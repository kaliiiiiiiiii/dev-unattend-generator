# Config files
Must be placed in `./config`

### [`StartPins.json`](/defaultconfig/StartPins.json)
![startLayout](assets/startLayout.png)
winndows 11 json for `StartPinSettings`
- [start layout example](https://learn.microsoft.com/en-us/windows/configuration/start/layout?tabs=intune-10%2Cintune-11&pivots=windows-11#start-layout-example)

The current startlayout can be dumped over PowerShell with [`Export-Startlayout -Path ./config/Startpint.json`](https://learn.microsoft.com/en-us/powershell/module/startlayout/export-startlayout)

### [`TaskBarIcons.xml`](/defaultconfig/TaskbarIcons.xml)
Icons to be dispayed on the TaskBar
![TaskBarIcons](assets/TaskBarIcons.png)

- [Taskbar Layout example](https://learn.microsoft.com/en-us/windows/configuration/taskbar/pinned-apps?tabs=intune&pivots=windows-11#taskbar-layout)
- [TaskbarLayout Schema Definition](https://learn.microsoft.com/en-us/windows/configuration/taskbar/xsd)