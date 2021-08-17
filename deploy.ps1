$tempDir = [System.IO.Path]::GetTempPath()
$tempName = [System.Guid]::NewGuid()


cdk deploy --require-approval-never --output (New-Item -ItemType Directory -Path (Join-Path $tempDir $tempName))