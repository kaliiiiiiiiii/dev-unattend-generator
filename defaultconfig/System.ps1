$LangList = Get-WinUserLanguageList
$enUS = $LangList | Where-Object { $_.LanguageTag -eq 'en-US' }
$enUS[0].InputMethodTips.Clear()
$enUS[0].InputMethodTips.Add('0409:00060409')
Set-WinUserLanguageList $LangList -Force