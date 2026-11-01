param(
    [string]$TrxPath = ".\ConnectaOficios.Tests\TestResults\resultado_pruebas.trx",
    [string]$OutputPath = ".\ConnectaOficios.Tests\TestResults\resultado_pruebas_detallado.html"
)

if (-not (Test-Path $TrxPath)) {
    Write-Error "No se encontró el archivo TRX: $TrxPath"
    exit 1
}

[xml]$trx = Get-Content -Path $TrxPath -Raw

$ns = New-Object System.Xml.XmlNamespaceManager($trx.NameTable)
$ns.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")

$resultSummary = $trx.SelectSingleNode("//t:ResultSummary", $ns)
$counters = $resultSummary.Counters

$total = [int]$counters.total
$passed = [int]$counters.passed
$failed = [int]$counters.failed
$skipped = [int]$counters.notExecuted

if ($total -gt 0) {
    $passPercentage = [math]::Round(($passed / $total) * 100, 2)
} else {
    $passPercentage = 0
}

$times = $trx.SelectSingleNode("//t:Times", $ns)
$startTime = $times.start
$finishTime = $times.finish

try {
    $duration = ([datetime]$finishTime - [datetime]$startTime)
    $durationText = "{0:N2} s" -f $duration.TotalSeconds
} catch {
    $durationText = "No disponible"
}

$results = $trx.SelectNodes("//t:UnitTestResult", $ns)

$rows = ""

foreach ($result in $results) {

    $testName = [System.Net.WebUtility]::HtmlEncode($result.testName)
    $outcome = $result.outcome
    $durationRaw = $result.duration

    switch ($outcome) {
        "Passed" {
            $statusClass = "passed"
            $statusText = "PASS"
        }

        "Failed" {
            $statusClass = "failed"
            $statusText = "FAIL"
        }

        default {
            $statusClass = "skipped"
            $statusText = "SKIPPED"
        }
    }

    $errorMessage = ""
    $stackTrace = ""

    $errorNode = $result.SelectSingleNode("t:Output/t:ErrorInfo", $ns)

    if ($errorNode) {
        if ($errorNode.Message) {
            $errorMessage = [System.Web.HttpUtility]::HtmlEncode($errorNode.Message)
        }

        if ($errorNode.StackTrace) {
            $stackTrace = [System.Web.HttpUtility]::HtmlEncode($errorNode.StackTrace)
        }
    }

    $details = ""

    if ($errorMessage -or $stackTrace) {
        $details = @"
<div class="error-box">
    <strong>Error:</strong>
    <pre>$errorMessage</pre>
    <strong>Stack trace:</strong>
    <pre>$stackTrace</pre>
</div>
"@
    }

    $rows += @"
<tr>
    <td>$testName</td>
    <td><span class="status $statusClass">$statusText</span></td>
    <td>$durationRaw</td>
    <td>$details</td>
</tr>
"@
}

$html = @"
<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">

<title>Reporte de Pruebas Unitarias - ConnectaOficios</title>

<style>

body {
    font-family: Arial, Helvetica, sans-serif;
    margin: 0;
    background: #f5f7fa;
    color: #1f2937;
}

.container {
    width: 92%;
    max-width: 1400px;
    margin: 35px auto;
}

.header {
    background: white;
    border-radius: 12px;
    padding: 28px 32px;
    margin-bottom: 24px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.header h1 {
    margin: 0 0 8px 0;
    font-size: 30px;
}

.header p {
    margin: 4px 0;
    color: #6b7280;
}

.summary-title {
    font-size: 22px;
    margin-bottom: 18px;
}

.cards {
    display: grid;
    grid-template-columns: repeat(5, 1fr);
    gap: 16px;
    margin-bottom: 30px;
}

.card {
    background: white;
    border-radius: 12px;
    padding: 20px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.card .label {
    color: #6b7280;
    font-size: 14px;
    margin-bottom: 8px;
}

.card .value {
    font-size: 28px;
    font-weight: bold;
}

.value.pass {
    color: #15803d;
}

.value.fail {
    color: #b91c1c;
}

.value.skip {
    color: #b45309;
}

.progress-container {
    background: #e5e7eb;
    height: 16px;
    border-radius: 999px;
    overflow: hidden;
    margin-top: 10px;
}

.progress-bar {
    height: 100%;
    width: ${passPercentage}%;
    background: #16a34a;
}

.section {
    background: white;
    border-radius: 12px;
    padding: 24px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

table {
    width: 100%;
    border-collapse: collapse;
    margin-top: 15px;
}

th {
    text-align: left;
    padding: 14px;
    background: #f3f4f6;
    border-bottom: 1px solid #d1d5db;
}

td {
    padding: 14px;
    border-bottom: 1px solid #e5e7eb;
    vertical-align: top;
}

.status {
    display: inline-block;
    padding: 5px 10px;
    border-radius: 999px;
    font-size: 12px;
    font-weight: bold;
}

.status.passed {
    background: #dcfce7;
    color: #166534;
}

.status.failed {
    background: #fee2e2;
    color: #991b1b;
}

.status.skipped {
    background: #fef3c7;
    color: #92400e;
}

.error-box {
    background: #fff1f2;
    border: 1px solid #fecdd3;
    padding: 12px;
    border-radius: 8px;
}

pre {
    white-space: pre-wrap;
    word-break: break-word;
    font-size: 12px;
}

.meta {
    display: flex;
    gap: 30px;
    flex-wrap: wrap;
    margin-top: 14px;
}

.meta div {
    color: #4b5563;
}

.footer {
    margin-top: 30px;
    text-align: center;
    color: #6b7280;
    font-size: 13px;
}

@media (max-width: 900px) {
    .cards {
        grid-template-columns: repeat(2, 1fr);
    }
}

</style>
</head>

<body>

<div class="container">

    <div class="header">
        <h1>Reporte de Pruebas Unitarias</h1>
        <p><strong>Proyecto:</strong> ConnectaOficios</p>
        <p><strong>Framework:</strong> .NET 8 / xUnit</p>

        <div class="meta">
            <div><strong>Inicio:</strong> $startTime</div>
            <div><strong>Finalización:</strong> $finishTime</div>
        </div>
    </div>

    <h2 class="summary-title">Resumen de ejecución</h2>

    <div class="cards">

        <div class="card">
            <div class="label">Total de pruebas</div>
            <div class="value">$total</div>
        </div>

        <div class="card">
            <div class="label">Aprobadas</div>
            <div class="value pass">$passed</div>
        </div>

        <div class="card">
            <div class="label">Fallidas</div>
            <div class="value fail">$failed</div>
        </div>

        <div class="card">
            <div class="label">Omitidas</div>
            <div class="value skip">$skipped</div>
        </div>

        <div class="card">
            <div class="label">Porcentaje de éxito</div>
            <div class="value">$passPercentage%</div>
            <div class="progress-container">
                <div class="progress-bar"></div>
            </div>
        </div>

    </div>

    <div class="section">

        <h2>Resultados de las pruebas</h2>

        <p>
            Duración total:
            <strong>$durationText</strong>
        </p>

        <table>

            <thead>
                <tr>
                    <th>Prueba</th>
                    <th>Estado</th>
                    <th>Duración</th>
                    <th>Información</th>
                </tr>
            </thead>

            <tbody>
                $rows
            </tbody>

        </table>

    </div>

    <div class="footer">
        Generado automáticamente a partir del archivo TRX producido por dotnet test.
    </div>

</div>

</body>
</html>
"@

$html | Out-File -FilePath $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "Reporte generado correctamente:"
Write-Host $OutputPath
Write-Host ""
Write-Host "Total:    $total"
Write-Host "Passed:   $passed"
Write-Host "Failed:   $failed"
Write-Host "Skipped:  $skipped"
Write-Host "Success:  $passPercentage%"
