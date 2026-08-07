#!/usr/bin/env bash
# EYE4PRO — Birleşik Codex Üretici
# Tüm codex + _index dosyalarını telefon/sohbet için tek belgede birleştirir.
# Kullanım: eye4pro/ kökünden ->  bash _ortak/birlesik-codex-uret.sh
set -euo pipefail

# Script nerede olursa olsun proje köküne geç (bu script _ortak/ altında)
KOK="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$KOK"

OUT="_ortak/eye4pro-tum-codex.md"
TARIH="$(date +%Y-%m-%d)"

# Dosya sırası: maincodex önce, sonra başlık başlık.
# Yeni modül codex'i eklediğinde bu listeye satır ekle.
FILES=(
  maincodex.md
  akustik/_index.md
  akustik/01-proje-geometri/codex.md
  akustik/02-delikli-panel/codex2.md
  akustik/03-ses-yalitimi/codex3.md
  akustik/04-ses-bariyeri/codex4.md
  akustik/05-darbe-yuzey/codex5.md
  akustik/06-oda-modlari/codex6.md
  akustik/07-yerinde-olcum/codex7.md
  akustik/08-bina-gurultu/codex8.md
  elektrik/_index.md
  elektrik/11-tek-hat-semasi/codex11.md
  elektrik/12-basit-proje-ciz/codex12.md
  elektrik/13-zayif-akim-tek-hat/codex13.md
  elektrik/14-kesif-hazirla/codex14.md
  elektrik/15-cevre-aydinlatma/codex15.md
  elektrik/16-zayif-akim-proje-ciz/codex16.md
  mekanik/_index.md
  mekanik/21-kanal-hesabi/codex21.md
  mimari/_index.md
  mimari/codex30.md
  mimari/mimari-modul-planlama-raporu.md
  mimari/31-ihtiyac-programi-mahal-sablonlari/codex31.md
  mimari/32-konsept-plan-parametrik-yerlesim/codex32.md
  mimari/33-dusey-cekirdek-saft-sirkulasyon/codex33.md
  mimari/34-yonetmelik-on-kontrol/codex34.md
  mimari/35-3d-bim-model/codex35.md
  mimari/36-detay-malzeme-yapi-elemani/codex36.md
  mimari/37-pafta-rapor-cizelgeler/codex37.md
  mimari/39-bim-koordinasyon-ihracat/codex39.md
  terrain/_index.md
  terrain/60-hafriyat-hesapla/codex60.md
  terrain/61-bina-temeli-kota-oturtma/codex61.md
  ortak-moduller/50-bina-geometri/codex50.md
  insaat/_index.md
)

{
  echo "# EYE4PRO — BİRLEŞİK CODEX (Telefon / Sohbet Sürümü)"
  echo
  echo "> Bu dosya tüm codex ve index dosyalarının tek belgede birleştirilmiş halidir."
  echo "> Üretim tarihi: $TARIH"
  echo "> Amaç: Claude mobil uygulamasına yükleyip masaüstü dışında planlama yapmak."
  echo "> Telefonda alınan kararlar bu dosyaya YAZILMAZ; masaya dönünce ilgili codex'e işlenir."
  echo
  echo "---"
  echo
  echo "## İçindekiler"
  echo
  n=1
  for f in "${FILES[@]}"; do
    echo "$n. \`$f\`"
    n=$((n+1))
  done
  echo
  echo "---"
  echo
  for f in "${FILES[@]}"; do
    if [[ ! -f "$f" ]]; then
      echo "⚠️  UYARI: dosya bulunamadı, atlandı -> $f" >&2
      continue
    fi
    echo
    echo "<!-- ============================================================ -->"
    echo "# 📄 DOSYA: \`$f\`"
    echo "<!-- ============================================================ -->"
    echo
    cat "$f"
    echo
  done
} > "$OUT"

echo "✅ Yazıldı: $OUT"
echo "   Satır: $(wc -l < "$OUT")  Boyut: $(ls -lh "$OUT" | awk '{print $5}')"
