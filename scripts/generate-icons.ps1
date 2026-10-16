# Rasterizes the editable SVG's rect/line/circle primitives with built-in Windows GDI+.
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$taskAssets=Join-Path (Split-Path $PSScriptRoot -Parent) 'src/ProcSight.App/Assets'
[xml]$taskSvg=Get-Content (Join-Path $taskAssets 'ProcSight.svg') -Raw
$taskFrames=@()
foreach($taskSize in @(16,20,24,32,40,48,64,256)) {
    $taskBitmap=[Drawing.Bitmap]::new($taskSize,$taskSize)
    $taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
    try {
        $taskGraphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $taskGraphics.ScaleTransform($taskSize/64.0,$taskSize/64.0)
        foreach($taskNode in $taskSvg.DocumentElement.ChildNodes) {
            if($taskNode.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
            switch($taskNode.LocalName) {
                'rect' { $taskBrush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($taskNode.fill)); try { $taskGraphics.FillRectangle($taskBrush,[single]$taskNode.x,[single]$taskNode.y,[single]$taskNode.width,[single]$taskNode.height) } finally { $taskBrush.Dispose() } }
                'circle' { $taskBrush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($taskNode.fill)); try { $taskRadius=[single]$taskNode.r; $taskGraphics.FillEllipse($taskBrush,[single]$taskNode.cx-$taskRadius,[single]$taskNode.cy-$taskRadius,2*$taskRadius,2*$taskRadius) } finally { $taskBrush.Dispose() } }
                'line' { $taskPen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($taskNode.stroke),[single]$taskNode.'stroke-width'); try { $taskGraphics.DrawLine($taskPen,[single]$taskNode.x1,[single]$taskNode.y1,[single]$taskNode.x2,[single]$taskNode.y2) } finally { $taskPen.Dispose() } }
                default { throw "Unsupported SVG primitive: $($taskNode.LocalName)" }
            }
        }
        $taskStream=[IO.MemoryStream]::new()
        try { $taskBitmap.Save($taskStream,[Drawing.Imaging.ImageFormat]::Png); $taskBytes=$taskStream.ToArray(); $taskFrames+=,@{Size=$taskSize;Bytes=$taskBytes}; [IO.File]::WriteAllBytes((Join-Path $taskAssets "ProcSight-$taskSize.png"),$taskBytes) } finally { $taskStream.Dispose() }
    } finally { $taskGraphics.Dispose(); $taskBitmap.Dispose() }
}
$taskFile=[IO.File]::Create((Join-Path $taskAssets 'ProcSight.ico')); $taskWriter=[IO.BinaryWriter]::new($taskFile)
try {
    $taskWriter.Write([uint16]0); $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]$taskFrames.Count)
    $taskOffset=6+16*$taskFrames.Count
    foreach($taskFrame in $taskFrames) {
        $taskDimension=if($taskFrame.Size -eq 256){0}else{$taskFrame.Size}
        $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([byte]$taskDimension); $taskWriter.Write([uint16]0)
        $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]32); $taskWriter.Write([uint32]$taskFrame.Bytes.Length); $taskWriter.Write([uint32]$taskOffset)
        $taskOffset+=$taskFrame.Bytes.Length
    }
    foreach($taskFrame in $taskFrames) { $taskWriter.Write([byte[]]$taskFrame.Bytes) }
} finally { $taskWriter.Dispose(); $taskFile.Dispose() }
Write-Output 'Generated 8 PNG sizes and the multi-resolution ICO from ProcSight.svg.'
