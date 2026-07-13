# find_titlekey_derivation.ps1
$encTitleKeyHex = "468935FAC3AA15A3916F718FBB2D7991"
$decTitleKeyHex = "2DCA220F5EE92F25C768AD02EC943FAD"

$encTitleKey = New-Object byte[] 16
for ($i = 0; $i -lt 16; $i++) {
    $encTitleKey[$i] = [Convert]::ToByte($encTitleKeyHex.Substring($i * 2, 2), 16)
}

$prodKeysPath = "E:\STORM SWITCH BOX\STORM_SWITCH_BOX+(1.1.000)\tools\prod.keys"
if (-not (Test-Path $prodKeysPath)) {
    Write-Error "prod.keys not found!"
    exit
}

$keys = @{}
foreach ($line in Get-Content $prodKeysPath) {
    $line = $line.Trim()
    if ($line.StartsWith("#") -or $line.StartsWith(";") -or -not $line.Contains("=")) { continue }
    $parts = $line.Split("=", 2)
    $name = $parts[0].Trim()
    $val = $parts[1].Trim().Split(" ")[0]
    if ($val.Length -eq 32) {
        $keys[$name] = $val
    }
}

Write-Output "Loaded $($keys.Count) keys. Testing ECB decryption..."

function Decrypt-AesEcb ($data, $keyBytes) {
    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.Mode = [System.Security.Cryptography.CipherMode]::ECB
    $aes.Padding = [System.Security.Cryptography.PaddingMode]::None
    $aes.Key = $keyBytes
    $decryptor = $aes.CreateDecryptor()
    return $decryptor.TransformFinalBlock($data, 0, 16)
}

$found = $false
foreach ($k in $keys.Keys) {
    $keyHex = $keys[$k]
    $keyBytes = New-Object byte[] 16
    for ($i = 0; $i -lt 16; $i++) {
        $keyBytes[$i] = [Convert]::ToByte($keyHex.Substring($i * 2, 2), 16)
    }
    
    try {
        $dec = Decrypt-AesEcb $encTitleKey $keyBytes
        $decHex = BitConverter::ToString($dec).Replace("-", "").ToUpper()
        if ($decHex -eq $decTitleKeyHex) {
            Write-Output "FOUND KEY: $k = $keyHex"
            $found = $true
            break
        }
    } catch {}
}

if (-not $found) {
    Write-Output "No matching key found in prod.keys."
}
