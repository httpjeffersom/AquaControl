#requires -RunAsAdministrator

[CmdletBinding()]
param(
    [string]$ExecutablePath = 'C:\Program Files\AquaControl\AquaControl.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-SignTool {
    $pathCommand = Get-Command 'signtool.exe' -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if ($pathCommand) {
        return $pathCommand.Source
    }

    $sdkRoots = [System.Collections.Generic.List[string]]::new()
    foreach ($programFiles in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if (-not $programFiles) {
            continue
        }

        foreach ($kitVersion in @('10', '8.1')) {
            $root = Join-Path $programFiles "Windows Kits\$kitVersion\bin"
            if ((Test-Path -LiteralPath $root -PathType Container) -and -not $sdkRoots.Contains($root)) {
                $sdkRoots.Add($root)
            }
        }
    }

    $candidates = @(
        foreach ($root in $sdkRoots) {
            Get-ChildItem -LiteralPath $root -Filter 'signtool.exe' -File -Recurse `
                -ErrorAction SilentlyContinue
        }
    )

    if ($candidates.Count -eq 0) {
        return $null
    }

    $preferred = $candidates | Sort-Object `
        @{ Expression = { if ($_.FullName -match '\\x64\\signtool\.exe$') { 0 } else { 1 } } },
        @{ Expression = {
            $match = [regex]::Match($_.FullName, '\\bin\\(?<version>\d+(?:\.\d+){1,3})\\')
            if ($match.Success) {
                try {
                    [version]$match.Groups['version'].Value
                }
                catch {
                    [version]'0.0'
                }
            }
            else {
                [version]'0.0'
            }
        }; Descending = $true } |
        Select-Object -First 1

    return $preferred.FullName
}

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "O executável não foi encontrado: $ExecutablePath"
}

$signToolPath = Find-SignTool
if (-not $signToolPath) {
    throw 'signtool.exe não foi encontrado no PATH nem nos diretórios conhecidos do Windows SDK. Instale o Windows SDK e tente novamente.'
}

$certificateFile = Join-Path $env:TEMP ("MeuDevCertificado-{0}.cer" -f [guid]::NewGuid())

try {
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject 'CN=MeuDevCertificado' `
        -CertStoreLocation 'Cert:\LocalMachine\My' `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(3)

    Export-Certificate -Cert $certificate -FilePath $certificateFile -Force | Out-Null

    Import-Certificate `
        -FilePath $certificateFile `
        -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null

    & $signToolPath sign `
        /fd SHA256 `
        /sha1 $certificate.Thumbprint `
        /s My `
        /sm `
        $ExecutablePath

    if ($LASTEXITCODE -ne 0) {
        throw "signtool.exe falhou ao assinar o executável (código $LASTEXITCODE)."
    }

    & $signToolPath verify /pa /v $ExecutablePath

    if ($LASTEXITCODE -ne 0) {
        throw "A verificação da assinatura falhou (código $LASTEXITCODE)."
    }

    Write-Host "Assinatura concluída e verificada: $ExecutablePath"
    Write-Host "Certificado: MeuDevCertificado ($($certificate.Thumbprint))"
}
finally {
    if (Test-Path -LiteralPath $certificateFile) {
        Remove-Item -LiteralPath $certificateFile -Force
    }
}
