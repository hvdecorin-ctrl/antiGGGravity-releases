$files = Get-ChildItem "*.xaml"
foreach ($f in $files) {
    $c = [IO.File]::ReadAllText($f.FullName)
    $r = [regex]::Replace($c, '(?s)>\s*<ComboBox\.ItemTemplate>.*?</ComboBox\.ItemTemplate>\s*</ComboBox>', ' />')
    [IO.File]::WriteAllText($f.FullName, $r)
}
