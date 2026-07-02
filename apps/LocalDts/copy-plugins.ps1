# 复制插件文件到WebDts.Blazor的Plugins目录

$sourceDirs = @(
    "Plugins\DataMigration.Plugin.AggregationTransformer\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.CsvSource\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.CsvTarget\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.EnumMappingTransformer\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.ExcelSource\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.MySqlSource\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.RenameTransformer\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.RulesEngineTransformer\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.SqlServerSource\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.SqliteSource\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.SqliteTarget\bin\Debug\net8.0",
    "Plugins\DataMigration.Plugin.SumTransformer\bin\Debug\net8.0",
    "DataMigration.Plugin.GeometryTransformer\bin\Debug\net8.0",
    "DataMigration.Plugin.DwgDataSource\bin\Debug\net8.0"
)

$targetDir = "WebDts.Blazor\Plugins"

# 确保目标目录存在
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force
}

# 复制每个插件的.dll文件
foreach ($sourceDir in $sourceDirs) {
    if (Test-Path $sourceDir) {
        $dllFiles = Get-ChildItem -Path $sourceDir -Filter "*.dll"
        foreach ($dllFile in $dllFiles) {
            $destinationPath = Join-Path $targetDir $dllFile.Name
            Copy-Item -Path $dllFile.FullName -Destination $destinationPath -Force
            Write-Host "复制: $($dllFile.FullName) -> $destinationPath"
        }
    } else {
        Write-Host "警告: 源目录不存在: $sourceDir"
    }
}

Write-Host "插件复制完成!"
