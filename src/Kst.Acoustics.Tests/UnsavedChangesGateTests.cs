using Kst.Acoustics.App.Views;

namespace Kst.Acoustics.Tests;

/// <summary>
/// UnsavedChangesGate, yıkıcı geçişlerden (proje açma, yeni içe aktarma, ana ekrana
/// dönme, pencere kapatma) önce ortak onay noktasıdır. Kirli olmayan durumda hiçbir
/// diyalog açmadan sessizce geçmeli — aksi halde her işlemde gereksiz bir onay
/// penceresi çıkar (maincodex M01 incelemesi P1).
/// </summary>
public static class UnsavedChangesGateTests
{
    public static void ProceedsWithoutPromptWhenNotDirty()
    {
        var saveCalled = false;

        var result = UnsavedChangesGate.ConfirmProceedAsync(
                null!,
                isDirty: false,
                saveAsync: () => { saveCalled = true; return Task.FromResult(true); })
            .GetAwaiter().GetResult();

        if (result != UnsavedProceedResult.Unchanged)
            throw new InvalidOperationException("isDirty=false iken sonuç Unchanged olmalı.");
        if (saveCalled)
            throw new InvalidOperationException("isDirty=false iken kaydetme çağrılmamalı (diyalog hiç açılmamalı).");
    }

    public static void DiscardIsDistinctFromSaveAndCancel()
    {
        var saveCalled = false;
        var discard = UnsavedChangesGate.ResolveChoiceAsync(
                UnsavedChangesChoice.Discard,
                () => { saveCalled = true; return Task.FromResult(true); })
            .GetAwaiter().GetResult();
        if (discard != UnsavedProceedResult.Discarded || saveCalled)
            throw new InvalidOperationException("Discard sonucu ayrı olmalı ve kayıt çağırmamalı.");

        var cancelledSave = UnsavedChangesGate.ResolveChoiceAsync(
                UnsavedChangesChoice.Save,
                () => Task.FromResult(false))
            .GetAwaiter().GetResult();
        if (cancelledSave != UnsavedProceedResult.Cancelled)
            throw new InvalidOperationException("Başarısız kayıt geçişi iptal etmeli.");
    }
}
