using System;

namespace Kst.Acoustics.App.Bina.Views;

/// Aktif editörün bekleyen değerini belge komutundan önce tam bir kez uygular.
/// UI türlerinden bağımsız olduğu için kısayol/closing sırası doğrudan test edilebilir.
public static class BinaEditorCommitYardimci
{
    public static bool Uygula(
        object? aktifEditor,
        params (object? Editor, Action Commit)[] editorler)
    {
        if (aktifEditor is null) return false;
        foreach (var (editor, commit) in editorler)
        {
            if (!ReferenceEquals(aktifEditor, editor)) continue;
            commit();
            return true;
        }
        return false;
    }
}

/// Canvas kontrolü oluşturulmadan sınanabilen karşılıklı dışlayan editör modları.
public readonly record struct BinaEditorModDurumu(bool OdaCizim, bool AciklikEkle)
{
    public static BinaEditorModDurumu Bos => new(false, false);
    public BinaEditorModDurumu OdaCiziminiBaslat() => new(true, false);
    public BinaEditorModDurumu AciklikEklemeyiBaslat() => new(false, true);
    public BinaEditorModDurumu TumunuIptalEt() => Bos;
}
