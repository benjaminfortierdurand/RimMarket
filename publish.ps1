# sync D:\RimWorldMods\RimMarket (dev) -> RimWorld\Mods\RimMarket (copie publiable, sans Source)
# relancer ce script avant chaque upload de mise a jour
$dev = "D:\RimWorldMods\RimMarket"
$pub = "D:\Steam\steamapps\common\RimWorld\Mods\RimMarket"

# garder l'id workshop ecrit par le jeu au premier upload
$pubId = Join-Path $pub "About\PublishedFileId.txt"
$devId = Join-Path $dev "About\PublishedFileId.txt"
if ((Test-Path $pubId) -and -not (Test-Path $devId)) { Copy-Item $pubId $devId }

# /R:3 /W:2: sans ca robocopy reessaie un million de fois toutes les 30s quand
# RimWorld est ouvert et verrouille la dll
robocopy $dev $pub /MIR /XD Source .research .git .claude _concept Screenshots /XF publish.ps1 *.md .gitignore LICENSE /R:3 /W:2 /NFL /NDL /NJH
if ($LASTEXITCODE -ge 8) {
    Write-Host "SYNC INCOMPLET: fichier verrouille (RimWorld ouvert ?). Ferme le jeu et relance."
} else {
    Write-Host "sync ok -> $pub"
}
Get-ChildItem $pub | Select-Object -ExpandProperty Name
