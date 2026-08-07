using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Kst.Acoustics.App.Views;

public enum UnsavedProceedResult
{
    /// <summary>Belge zaten temizdi; kullanıcıya soru sorulmadı.</summary>
    Unchanged,
    /// <summary>Kullanıcı değişiklikleri başarıyla kaydetti.</summary>
    Saved,
    /// <summary>Kullanıcı değişiklikleri açıkça atmayı seçti.</summary>
    Discarded,
    /// <summary>Kullanıcı işlemi iptal etti veya kayıt başarısız oldu.</summary>
    Cancelled
}

/// <summary>
/// Yıkıcı geçişlerden (başka proje açma, yeni içe aktarma, ana ekrana dönme, pencere
/// kapatma) önce ortak "Kaydet / Kaydetmeden Çık / İptal" onay akışı. Her pencere kodu
/// bu tek yol üzerinden geçer ki hiçbiri kaydedilmemiş değişikliği sessizce kaybetmesin.
/// </summary>
public static class UnsavedChangesGate
{
    public static async Task<UnsavedProceedResult> ConfirmProceedAsync(
        Window owner,
        bool isDirty,
        Func<Task<bool>> saveAsync,
        string message = "Kaydedilmemiş değişiklikler var. Ne yapmak istersiniz?")
    {
        if (!isDirty) return UnsavedProceedResult.Unchanged;

        var choice = await UnsavedChangesDialog.ShowAsync(owner, message);
        return await ResolveChoiceAsync(choice, saveAsync);
    }

    public static async Task<UnsavedProceedResult> ResolveChoiceAsync(
        UnsavedChangesChoice choice,
        Func<Task<bool>> saveAsync)
    {
        return choice switch
        {
            UnsavedChangesChoice.Save    => await saveAsync()
                ? UnsavedProceedResult.Saved
                : UnsavedProceedResult.Cancelled,
            UnsavedChangesChoice.Discard => UnsavedProceedResult.Discarded,
            _                            => UnsavedProceedResult.Cancelled
        };
    }
}
