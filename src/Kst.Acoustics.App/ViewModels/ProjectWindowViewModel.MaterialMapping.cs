using System.Collections.ObjectModel;
using Kst.Acoustics.Core.Projects;

namespace Kst.Acoustics.App.ViewModels;

public sealed partial class ProjectWindowViewModel
{
    private readonly MaterialMappingTemplateService _materialMappingService = new();

    public ObservableCollection<MaterialMappingTemplate> MaterialMappingTemplates { get; } = [];

    public MaterialMappingTemplate? SelectedMaterialMappingTemplate
    {
        get => _selectedMaterialMappingTemplate;
        set => SetProperty(ref _selectedMaterialMappingTemplate, value);
    }
    private MaterialMappingTemplate? _selectedMaterialMappingTemplate;

    public string NewMaterialMappingTemplateName
    {
        get => _newMaterialMappingTemplateName;
        set => SetProperty(ref _newMaterialMappingTemplateName, value);
    }
    private string _newMaterialMappingTemplateName = "Proje eşlemesi";

    public string MaterialMappingStatus
    {
        get => _materialMappingStatus;
        private set => SetProperty(ref _materialMappingStatus, value);
    }
    private string _materialMappingStatus = "Katman adlarından otomatik materyal eşleyebilirsiniz.";

    private async void InitMaterialMappingTemplates()
    {
        try
        {
            var templates = await _materialMappingService.LoadAsync();
            MaterialMappingTemplates.Clear();
            foreach (var template in templates) MaterialMappingTemplates.Add(template);
            SelectedMaterialMappingTemplate = MaterialMappingTemplates.FirstOrDefault();
        }
        catch (Exception ex)
        {
            MaterialMappingStatus = $"Materyal eşleme şablonları yüklenemedi: {ex.Message}";
        }
    }

    public void ApplySelectedMaterialMappingTemplate()
    {
        var template = SelectedMaterialMappingTemplate;
        if (template is null)
        {
            MaterialMappingStatus = "Önce bir eşleme şablonu seçin.";
            return;
        }

        var materials = MaterialDatabase.Instance.All;
        var available = materials.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = _materialMappingService.Apply(template, _workingSurfaces, available);
        var pending = new List<(CategoryMaterialViewModel Row, AcousticMaterial Material)>();
        foreach (var match in matches)
        {
            var row = CategoryMaterialRows.FirstOrDefault(r =>
                r.Category.Equals(match.Category, StringComparison.OrdinalIgnoreCase));
            var material = MaterialDatabase.Instance.FindById(match.MaterialId) ??
                           MaterialLibrary.FindById(match.MaterialId);
            if (row is null || material is null || row.SelectedMaterial.Id == material.Id) continue;
            pending.Add((row, material));
        }

        if (pending.Count > 0)
        {
            using var historyChange = BeginDocumentChange($"'{template.Name}' materyal şablonu uygulandı");
            foreach (var (row, material) in pending)
                row.SelectedMaterial = material;
        }
        RefreshMaterialStatus();
        MaterialMappingStatus = pending.Count == 0
            ? "Şablon uygulandı; değişecek kategori bulunamadı."
            : $"{pending.Count} kategoriye şablondan materyal atandı.";
        StatusMessage = MaterialMappingStatus;
    }

    public async Task SaveCurrentMaterialMappingTemplateAsync()
    {
        try
        {
            var template = MaterialMappingTemplateService.FromExactCategoryAssignments(
                NewMaterialMappingTemplateName,
                BuildCategoryMaterialMap());
            var users = MaterialMappingTemplates.Where(t => !t.IsBuiltIn).Append(template).ToArray();
            await _materialMappingService.SaveUserTemplatesAsync(users);
            MaterialMappingTemplates.Add(template);
            SelectedMaterialMappingTemplate = template;
            MaterialMappingStatus = $"'{template.Name}' eşleme şablonu kaydedildi.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException)
        {
            MaterialMappingStatus = $"Eşleme şablonu kaydedilemedi: {ex.Message}";
        }
    }

    public async Task DeleteSelectedMaterialMappingTemplateAsync()
    {
        var selected = SelectedMaterialMappingTemplate;
        if (selected is null || selected.IsBuiltIn)
        {
            MaterialMappingStatus = "Yerleşik şablon silinemez.";
            return;
        }

        MaterialMappingTemplates.Remove(selected);
        await _materialMappingService.SaveUserTemplatesAsync(
            MaterialMappingTemplates.Where(t => !t.IsBuiltIn));
        SelectedMaterialMappingTemplate = MaterialMappingTemplates.FirstOrDefault();
        MaterialMappingStatus = $"'{selected.Name}' şablonu silindi.";
    }
}
