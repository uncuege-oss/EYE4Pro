using System.Collections.ObjectModel;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed class MaterialLibraryWindowViewModel : ViewModelBase
{
    private readonly MaterialDatabase _db = MaterialDatabase.Instance;

    public MaterialLibraryWindowViewModel()
    {
        Reload();
    }

    // ── Liste ─────────────────────────────────────────────────────────────────

    public ObservableCollection<MaterialRowViewModel> Rows { get; } = [];

    public MaterialRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value))
            {
                LoadEditor(value?.Material);
                OnPropertyChanged(nameof(CanDelete));
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(IsEditorReadOnly));
            }
        }
    }
    private MaterialRowViewModel? _selectedRow;

    public bool CanDelete => SelectedRow?.IsCustom == true;
    public bool IsEditorReadOnly => !IsEditorForNew && SelectedRow?.IsCustom != true;

    // ── Editör alanları ───────────────────────────────────────────────────────

    public string EditId          { get => _editId;   set { SetProperty(ref _editId,   value); OnPropertyChanged(nameof(CanSave)); } }
    public string EditName        { get => _editName; set { SetProperty(ref _editName, value); OnPropertyChanged(nameof(CanSave)); } }
    public double EditAlpha63     { get => _ea0; set => SetProperty(ref _ea0, Clamp(value)); }
    public double EditAlpha125    { get => _ea1; set => SetProperty(ref _ea1, Clamp(value)); }
    public double EditAlpha250    { get => _ea2; set => SetProperty(ref _ea2, Clamp(value)); }
    public double EditAlpha500    { get => _ea3; set => SetProperty(ref _ea3, Clamp(value)); }
    public double EditAlpha1k     { get => _ea4; set => SetProperty(ref _ea4, Clamp(value)); }
    public double EditAlpha2k     { get => _ea5; set => SetProperty(ref _ea5, Clamp(value)); }
    public double EditAlpha4k     { get => _ea6; set => SetProperty(ref _ea6, Clamp(value)); }
    public double EditAlpha8k     { get => _ea7; set => SetProperty(ref _ea7, Clamp(value)); }
    public double EditScattering  { get => _es;  set => SetProperty(ref _es,  Clamp(value)); }
    public string EditCategory     { get => _editCategory; set => SetProperty(ref _editCategory, value); }
    public string EditNote         { get => _editNote; set => SetProperty(ref _editNote, value); }
    public string EditReference    { get => _editReference; set => SetProperty(ref _editReference, value); }
    public string EditScatteringDataType { get => _editScatteringDataType; set => SetProperty(ref _editScatteringDataType, value); }

    private string _editId = string.Empty;
    private string _editName = string.Empty;
    private string _editCategory = string.Empty;
    private string _editNote = string.Empty;
    private string _editReference = string.Empty;
    private string _editScatteringDataType = string.Empty;
    private double _ea0, _ea1, _ea2, _ea3, _ea4, _ea5, _ea6, _ea7, _es;

    public bool IsEditorForNew
    {
        get => _isEditorForNew;
        private set
        {
            SetProperty(ref _isEditorForNew, value);
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsEditorReadOnly));
        }
    }
    private bool _isEditorForNew;

    public bool CanSave => !string.IsNullOrWhiteSpace(EditId) && !string.IsNullOrWhiteSpace(EditName)
                           && (IsEditorForNew || SelectedRow?.IsCustom == true);

    // ── Durum ─────────────────────────────────────────────────────────────────

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }
    private string _statusText = $"{MaterialDatabase.Instance.All.Count} materyal yüklendi.";

    // ── Komutlar ──────────────────────────────────────────────────────────────

    public void StartNew()
    {
        SelectedRow    = null;
        IsEditorForNew = true;
        EditId = EditName = string.Empty;
        EditCategory = EditNote = EditReference = EditScatteringDataType = string.Empty;
        _ea0 = _ea1 = _ea2 = _ea3 = _ea4 = _ea5 = _ea6 = _ea7 = 0.05;
        _es  = 0.05;
        RefreshEditorBindings();
    }

    public void SaveCurrent()
    {
        if (!CanSave) return;

        var mat = BuildFromEditor();
        if (IsEditorForNew)
        {
            if (!_db.Add(mat))
            { StatusText = $"⚠ '{mat.Id}' ID'si zaten mevcut."; return; }
        }
        else
        {
            _db.Update(mat);
        }

        Reload();
        StatusText = $"'{mat.DisplayName}' kaydedildi.";
        IsEditorForNew = false;
    }

    public void DeleteSelected()
    {
        if (SelectedRow is not { IsCustom: true } row) return;
        _db.Remove(row.Material.Id);
        Reload();
        StatusText = $"'{row.Material.DisplayName}' silindi.";
    }

    // ── Excel ─────────────────────────────────────────────────────────────────

    public void ExportAll(string filePath)
    {
        MaterialExcelService.Export(filePath, _db.All);
        StatusText = $"Tüm materyaller dışa aktarıldı: {filePath}";
    }

    public void ExportTemplate(string filePath)
    {
        MaterialExcelService.ExportTemplate(filePath);
        StatusText = $"Şablon oluşturuldu: {filePath}";
    }

    public string ImportFromExcel(string filePath)
    {
        var (materials, errors) = MaterialExcelService.Import(filePath);
        var (added, updated)    = _db.Merge(materials);
        Reload();

        var msg = $"{added} eklendi, {updated} güncellendi.";
        if (errors.Count > 0) msg += $" {errors.Count} hata: {errors[0]}";
        StatusText = msg;
        return msg;
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    private void Reload()
    {
        Rows.Clear();
        foreach (var m in _db.BuiltIn)
            Rows.Add(new MaterialRowViewModel(m, false, _db.IsFavorite(m.Id), ToggleFavorite));
        foreach (var m in _db.Custom)
            Rows.Add(new MaterialRowViewModel(m, true, _db.IsFavorite(m.Id), ToggleFavorite));
    }

    private void ToggleFavorite(MaterialRowViewModel row)
    {
        row.IsFavorite = !row.IsFavorite;
        _db.SetFavorite(row.Id, row.IsFavorite);
        StatusText = row.IsFavorite
            ? $"'{row.Name}' favorilere eklendi."
            : $"'{row.Name}' favorilerden çıkarıldı.";
    }

    private void LoadEditor(AcousticMaterial? m)
    {
        IsEditorForNew = false;
        if (m is null) return;

        EditId   = m.Id;
        EditName = m.DisplayName;
        EditCategory = m.Category;
        EditNote = m.Note;
        EditReference = m.Reference;
        EditScatteringDataType = m.ScatteringDataType;
        _ea0 = m.Alpha[0]; _ea1 = m.Alpha[1]; _ea2 = m.Alpha[2]; _ea3 = m.Alpha[3];
        _ea4 = m.Alpha[4]; _ea5 = m.Alpha[5]; _ea6 = m.Alpha[6]; _ea7 = m.Alpha[7];
        _es  = m.ScatteringMid;
        RefreshEditorBindings();
    }

    private void RefreshEditorBindings()
    {
        OnPropertyChanged(nameof(EditId));   OnPropertyChanged(nameof(EditName));
        OnPropertyChanged(nameof(EditAlpha63));  OnPropertyChanged(nameof(EditAlpha125));
        OnPropertyChanged(nameof(EditAlpha250)); OnPropertyChanged(nameof(EditAlpha500));
        OnPropertyChanged(nameof(EditAlpha1k));  OnPropertyChanged(nameof(EditAlpha2k));
        OnPropertyChanged(nameof(EditAlpha4k));  OnPropertyChanged(nameof(EditAlpha8k));
        OnPropertyChanged(nameof(EditScattering));
        OnPropertyChanged(nameof(EditCategory)); OnPropertyChanged(nameof(EditNote));
        OnPropertyChanged(nameof(EditReference)); OnPropertyChanged(nameof(EditScatteringDataType));
    }

    private AcousticMaterial BuildFromEditor() =>
        new(EditId.Trim(), EditName.Trim(),
            [EditAlpha63, EditAlpha125, EditAlpha250, EditAlpha500,
             EditAlpha1k, EditAlpha2k,  EditAlpha4k,  EditAlpha8k],
            EditScattering,
            EditCategory.Trim(),
            EditNote.Trim(),
            EditReference.Trim(),
            EditScatteringDataType.Trim());

    private static double Clamp(double v) => Math.Clamp(v, 0, 1);
}

public sealed class MaterialRowViewModel(
    AcousticMaterial material,
    bool isCustom,
    bool isFavorite,
    Action<MaterialRowViewModel> toggleFavorite) : ViewModelBase
{
    public AcousticMaterial Material  { get; } = material;
    public bool             IsCustom  { get; } = isCustom;
    public string           TypeLabel => IsCustom ? "Özel" : "Yerleşik";
    public string           Id        => Material.Id;
    public string           Name      => Material.DisplayName;
    public string           Category  => string.IsNullOrWhiteSpace(Material.Category) ? "-" : Material.Category;
    public string           Reference => string.IsNullOrWhiteSpace(Material.Reference) ? "-" : Material.Reference;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteGlyph));
            }
        }
    }
    private bool _isFavorite = isFavorite;

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";

    public void ToggleFavorite() => toggleFavorite(this);

    public string           A63       => Material.Alpha[0].ToString("F2");
    public string           A125      => Material.Alpha[1].ToString("F2");
    public string           A250      => Material.Alpha[2].ToString("F2");
    public string           A500      => Material.Alpha[3].ToString("F2");
    public string           A1k       => Material.Alpha[4].ToString("F2");
    public string           A2k       => Material.Alpha[5].ToString("F2");
    public string           A4k       => Material.Alpha[6].ToString("F2");
    public string           A8k       => Material.Alpha[7].ToString("F2");
    public string           Scat      => Material.ScatteringMid.ToString("F2");
}
